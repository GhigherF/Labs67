using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;

namespace HotelBooking;

public sealed class EventBroker : BackgroundService
{
    private const int DefaultMaximumAttempts = 3;
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan BrokerUnavailableDelay = TimeSpan.FromMilliseconds(250);

    private readonly object _sync = new();
    private readonly HashSet<string> _eventTypes = new(
        DomainEventTypes.All,
        StringComparer.Ordinal
    );
    private readonly Dictionary<string, Dictionary<string, IEventSubscriber>> _subscribers = new(
        StringComparer.Ordinal
    );
    private readonly ConcurrentDictionary<Guid, EventState> _events = new();
    private readonly Channel<DeliveryWorkItem> _queue = Channel.CreateUnbounded<DeliveryWorkItem>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false }
    );
    private readonly ILogger<EventBroker> _logger;
    private readonly int _workerCount;
    private readonly int _maximumAttempts;
    private readonly TimeSpan _retryDelay;
    private volatile bool _isAvailable = true;
    private int _pendingDeliveries;

    public EventBroker(
        string brokerId,
        ILogger<EventBroker> logger,
        int workerCount = 8,
        int maximumAttempts = DefaultMaximumAttempts,
        TimeSpan? retryDelay = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(brokerId);
        ArgumentOutOfRangeException.ThrowIfLessThan(workerCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumAttempts, 1);

        BrokerId = brokerId;
        _logger = logger;
        _workerCount = workerCount;
        _maximumAttempts = maximumAttempts;
        _retryDelay = retryDelay ?? DefaultRetryDelay;
        ArgumentOutOfRangeException.ThrowIfLessThan(_retryDelay, TimeSpan.Zero);
    }

    public string BrokerId { get; }
    public bool IsAvailable => _isAvailable;
    public int PendingDeliveries => Math.Max(0, Volatile.Read(ref _pendingDeliveries));

    public bool Subscribe(string eventType, IEventSubscriber subscriber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentNullException.ThrowIfNull(subscriber);

        if (string.IsNullOrWhiteSpace(subscriber.Name))
            throw new ArgumentException("Subscriber must have a name.", nameof(subscriber));

        lock (_sync)
        {
            if (!_eventTypes.Contains(eventType))
                throw new ArgumentException(
                    $"Unsupported event type: {eventType}",
                    nameof(eventType)
                );

            if (!_subscribers.TryGetValue(eventType, out var subscribers))
            {
                subscribers = new Dictionary<string, IEventSubscriber>(StringComparer.Ordinal);
                _subscribers.Add(eventType, subscribers);
            }

            if (!subscribers.TryAdd(subscriber.Name, subscriber))
            {
                _logger.LogWarning(
                    "Broker {BrokerId}: subscriber {Subscriber} is already registered for {EventType}",
                    BrokerId,
                    subscriber.Name,
                    eventType
                );
                return false;
            }

            _logger.LogInformation(
                "Broker {BrokerId}: subscriber {Subscriber} subscribed to {EventType}",
                BrokerId,
                subscriber.Name,
                eventType
            );
            return true;
        }
    }

    public bool Unsubscribe(string eventType, string subscriberName)
    {
        lock (_sync)
        {
            return _subscribers.TryGetValue(eventType, out var subscribers)
                && subscribers.Remove(subscriberName);
        }
    }

    public IReadOnlyList<EventSubscriptionInfo> GetSubscriptions()
    {
        lock (_sync)
        {
            return _eventTypes
                .OrderBy(eventType => eventType, StringComparer.Ordinal)
                .Select(eventType =>
                {
                    var names = _subscribers.TryGetValue(eventType, out var subscribers)
                        ? subscribers.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray()
                        : Array.Empty<string>();

                    return new EventSubscriptionInfo(BrokerId, eventType, names);
                })
                .ToArray();
        }
    }

    public void SetAvailable(bool available)
    {
        _isAvailable = available;
        _logger.LogWarning(
            "Broker {BrokerId} availability changed: {State}",
            BrokerId,
            available ? "AVAILABLE" : "UNAVAILABLE"
        );
    }

    public Task<EventPublishResult> PublishAsync(DomainEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!_isAvailable)
            throw new InvalidOperationException($"Broker {BrokerId} is UNAVAILABLE.");

        IEventSubscriber[] recipients;
        lock (_sync)
        {
            if (!_eventTypes.Contains(message.EventType))
                throw new ArgumentException(
                    $"Unsupported event type: {message.EventType}",
                    nameof(message)
                );

            recipients = _subscribers.TryGetValue(message.EventType, out var subscribers)
                ? subscribers.Values.ToArray()
                : Array.Empty<IEventSubscriber>();
        }

        if (!_isAvailable)
            throw new InvalidOperationException($"Broker {BrokerId} is UNAVAILABLE.");

        var queuedAt = DateTimeOffset.UtcNow;
        var eventState = new EventState(BrokerId, message, queuedAt);
        if (!_events.TryAdd(message.EventId, eventState))
            throw new InvalidOperationException($"Event already exists: {message.EventId}");

        _logger.LogInformation(
            "Broker {BrokerId} received event {EventType}; EventId={EventId}, ClientId={ClientId}",
            BrokerId,
            message.EventType,
            message.EventId,
            message.ClientId
        );

        foreach (var subscriber in recipients)
        {
            eventState.Deliveries[subscriber.Name] = new SubscriberDeliveryResult(
                message.EventId,
                message.EventType,
                message.EntityId,
                message.ClientId,
                BrokerId,
                subscriber.Name,
                "QUEUED",
                0,
                queuedAt,
                null,
                null,
                null,
                null
            );

            Interlocked.Increment(ref _pendingDeliveries);
            if (!_queue.Writer.TryWrite(new DeliveryWorkItem(eventState, subscriber, 0)))
            {
                Interlocked.Decrement(ref _pendingDeliveries);
                eventState.Deliveries[subscriber.Name] = eventState.Deliveries[subscriber.Name]
                    with
                    {
                        Status = "FAILED",
                        CompletedAt = DateTimeOffset.UtcNow,
                        LastError = "Broker queue is closed.",
                    };
            }
        }

        return Task.FromResult(
            new EventPublishResult(message.EventId, true, BrokerId, recipients.Length)
        );
    }

    public IReadOnlyList<EventDeliveryReport> GetDeliveryReports()
    {
        return _events.Values
            .Select(CreateReport)
            .OrderByDescending(report => report.QueuedAt)
            .ToArray();
    }

    public EventDeliveryReport? GetDeliveryReport(Guid eventId) =>
        _events.TryGetValue(eventId, out var eventState) ? CreateReport(eventState) : null;

    public BrokerStatus GetStatus() =>
        new(BrokerId, _isAvailable ? "AVAILABLE" : "UNAVAILABLE", PendingDeliveries);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workers = Enumerable
            .Range(0, _workerCount)
            .Select(_ => ProcessQueueAsync(stoppingToken));

        await Task.WhenAll(workers);
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        await foreach (var item in _queue.Reader.ReadAllAsync(cancellationToken))
        {
            await ProcessDeliveryAsync(item, cancellationToken);
        }
    }

    private async Task ProcessDeliveryAsync(
        DeliveryWorkItem item,
        CancellationToken cancellationToken
    )
    {
        while (!_isAvailable)
            await Task.Delay(BrokerUnavailableDelay, cancellationToken);

        var attempt = item.Attempt + 1;
        var startedAt = DateTimeOffset.UtcNow;
        var previous = item.EventState.Deliveries[item.Subscriber.Name];
        item.EventState.Deliveries[item.Subscriber.Name] = previous with
        {
            Status = "PROCESSING",
            Attempts = attempt,
            StartedAt = startedAt,
            CompletedAt = null,
            DurationMilliseconds = null,
        };

        _logger.LogInformation(
            "Broker {BrokerId} -> {Subscriber} | START | EventType={EventType}, EventId={EventId}, ClientId={ClientId}, Attempt={Attempt}/{MaximumAttempts}",
            BrokerId,
            item.Subscriber.Name,
            item.EventState.Message.EventType,
            item.EventState.Message.EventId,
            item.EventState.Message.ClientId,
            attempt,
            _maximumAttempts
        );

        try
        {
            await item.Subscriber.HandleAsync(item.EventState.Message);

            var completedAt = DateTimeOffset.UtcNow;
            item.EventState.Deliveries[item.Subscriber.Name] = item.EventState.Deliveries[
                item.Subscriber.Name
            ] with
            {
                Status = "DELIVERED",
                CompletedAt = completedAt,
                DurationMilliseconds = (completedAt - startedAt).TotalMilliseconds,
                LastError = null,
            };

            Interlocked.Decrement(ref _pendingDeliveries);
            _logger.LogInformation(
                "Broker {BrokerId} -> {Subscriber} | SUCCESS | EventId={EventId}, Attempt={Attempt}",
                BrokerId,
                item.Subscriber.Name,
                item.EventState.Message.EventId,
                attempt
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failedAt = DateTimeOffset.UtcNow;
            var duration = (failedAt - startedAt).TotalMilliseconds;
            var shouldRetry = attempt < _maximumAttempts;

            item.EventState.Deliveries[item.Subscriber.Name] = item.EventState.Deliveries[
                item.Subscriber.Name
            ] with
            {
                Status = shouldRetry ? "RETRYING" : "FAILED",
                CompletedAt = failedAt,
                DurationMilliseconds = duration,
                LastError = exception.Message,
            };

            _logger.LogError(
                exception,
                "Broker {BrokerId} -> {Subscriber} | {Status} | EventType={EventType}, EventId={EventId}, ClientId={ClientId}, Attempt={Attempt}/{MaximumAttempts}",
                BrokerId,
                item.Subscriber.Name,
                shouldRetry ? "RETRY" : "FAILED",
                item.EventState.Message.EventType,
                item.EventState.Message.EventId,
                item.EventState.Message.ClientId,
                attempt,
                _maximumAttempts
            );

            if (shouldRetry)
            {
                await Task.Delay(_retryDelay, cancellationToken);
                item.EventState.Deliveries[item.Subscriber.Name] = item.EventState.Deliveries[
                    item.Subscriber.Name
                ] with
                {
                    Status = "QUEUED",
                };

                if (_queue.Writer.TryWrite(item with { Attempt = attempt }))
                    return;

                item.EventState.Deliveries[item.Subscriber.Name] = item.EventState.Deliveries[
                    item.Subscriber.Name
                ] with
                {
                    Status = "FAILED",
                    CompletedAt = DateTimeOffset.UtcNow,
                    LastError = "Broker queue is closed.",
                };
            }

            Interlocked.Decrement(ref _pendingDeliveries);
        }
    }

    private static EventDeliveryReport CreateReport(EventState state)
    {
        var deliveries = state.Deliveries.Values
            .OrderBy(result => result.SubscriberName, StringComparer.Ordinal)
            .ToArray();

        var status = deliveries.Length == 0
            ? "NO_SUBSCRIBERS"
            : deliveries.All(result => result.Status == "DELIVERED")
                ? "DELIVERED"
                : deliveries.Any(result =>
                    result.Status is "QUEUED" or "PROCESSING" or "RETRYING"
                )
                    ? "PENDING"
                    : deliveries.Any(result => result.Status == "DELIVERED")
                        ? "PARTIAL"
                        : "FAILED";

        return new EventDeliveryReport(
            state.Message,
            state.BrokerId,
            state.QueuedAt,
            status,
            deliveries
        );
    }

    private sealed class EventState(string brokerId, DomainEvent message, DateTimeOffset queuedAt)
    {
        public string BrokerId { get; } = brokerId;
        public DomainEvent Message { get; } = message;
        public DateTimeOffset QueuedAt { get; } = queuedAt;
        public ConcurrentDictionary<string, SubscriberDeliveryResult> Deliveries { get; } = new(
            StringComparer.Ordinal
        );
    }

    private sealed record DeliveryWorkItem(
        EventState EventState,
        IEventSubscriber Subscriber,
        int Attempt
    );
}
