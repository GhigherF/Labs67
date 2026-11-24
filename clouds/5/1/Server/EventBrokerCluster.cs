using Microsoft.Extensions.Hosting;

namespace HotelBooking;

public sealed class EventBrokerCluster : IHostedService, IDisposable
{
    private readonly EventBroker[] _brokers;
    private readonly ILogger<EventBrokerCluster> _logger;
    private int _nextBroker;

    public EventBrokerCluster(
        IEnumerable<EventBroker> brokers,
        ILogger<EventBrokerCluster> logger
    )
    {
        _brokers = brokers.ToArray();
        _logger = logger;
        if (_brokers.Length == 0)
            throw new ArgumentException("At least one Broker is required.", nameof(brokers));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var broker in _brokers)
            await broker.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var broker in _brokers)
            await broker.StopAsync(cancellationToken);
    }

    public void Dispose()
    {
        foreach (var broker in _brokers)
            broker.Dispose();
    }

    public bool Subscribe(string eventType, IEventSubscriber subscriber)
    {
        var registered = false;
        foreach (var broker in _brokers)
            registered |= broker.Subscribe(eventType, subscriber);
        return registered;
    }

    public bool Unsubscribe(string eventType, string subscriberName)
    {
        var removed = false;
        foreach (var broker in _brokers)
            removed |= broker.Unsubscribe(eventType, subscriberName);
        return removed;
    }

    public async Task<EventPublishResult> PublishAsync(DomainEvent message)
    {
        var firstIndex = (int)((uint)Interlocked.Increment(ref _nextBroker) % (uint)_brokers.Length);
        Exception? lastError = null;

        for (var offset = 0; offset < _brokers.Length; offset++)
        {
            var broker = _brokers[(firstIndex + offset) % _brokers.Length];
            if (!broker.IsAvailable)
                continue;

            try
            {
                var result = await broker.PublishAsync(message);
                _logger.LogInformation(
                    "Publisher -> Broker {BrokerId} | ACCEPTED | EventId={EventId}, ClientId={ClientId}",
                    broker.BrokerId,
                    message.EventId,
                    message.ClientId
                );
                return result;
            }
            catch (Exception exception)
            {
                lastError = exception;
                _logger.LogError(
                    exception,
                    "Publisher could not publish EventId={EventId} to Broker {BrokerId}",
                    message.EventId,
                    broker.BrokerId
                );
            }
        }

        var error = lastError?.Message ?? "No available brokers.";
        _logger.LogError(
            "Publisher -> Broker | FAILED | EventId={EventId}, ClientId={ClientId}, Error={Error}",
            message.EventId,
            message.ClientId,
            error
        );
        return new EventPublishResult(message.EventId, false, null, 0, error);
    }

    public IReadOnlyList<BrokerStatus> GetBrokerStatuses() =>
        _brokers.Select(broker => broker.GetStatus()).ToArray();

    public BrokerStatus SetBrokerAvailability(string brokerId, bool available)
    {
        var broker = FindBroker(brokerId);
        broker.SetAvailable(available);
        return broker.GetStatus();
    }

    public IReadOnlyList<EventSubscriptionInfo> GetSubscriptions() =>
        _brokers.SelectMany(broker => broker.GetSubscriptions()).ToArray();

    public IReadOnlyList<EventDeliveryReport> GetDeliveryReports() =>
        _brokers.SelectMany(broker => broker.GetDeliveryReports())
            .OrderByDescending(report => report.QueuedAt)
            .ToArray();

    public EventDeliveryReport? GetDeliveryReport(Guid eventId) =>
        _brokers.Select(broker => broker.GetDeliveryReport(eventId))
            .FirstOrDefault(report => report is not null);

    private EventBroker FindBroker(string brokerId) =>
        _brokers.FirstOrDefault(broker =>
            string.Equals(broker.BrokerId, brokerId, StringComparison.OrdinalIgnoreCase)
        )
        ?? throw new ArgumentException($"Unknown broker: {brokerId}", nameof(brokerId));
}
