namespace HotelBooking;

public abstract class EventSubscriberBase(
    string name,
    IReadOnlyCollection<string> eventTypes,
    TimeSpan processingDelay,
    ILogger logger
) : IControllableEventSubscriber
{
    private const int MaximumStoredEvents = 100;
    private readonly object _sync = new();
    private readonly Queue<DomainEvent> _receivedEvents = new();
    private volatile bool _isAvailable = true;
    private int _failuresRemaining;

    public string Name { get; } = name;
    public IReadOnlyCollection<string> EventTypes { get; } = eventTypes;
    public bool IsAvailable => _isAvailable;
    public string State => _isAvailable ? "AVAILABLE" : "UNAVAILABLE";

    public int ReceivedCount
    {
        get
        {
            lock (_sync)
                return _receivedEvents.Count;
        }
    }

    public async Task HandleAsync(DomainEvent message)
    {
        var startedAt = DateTimeOffset.UtcNow;
        logger.LogInformation(
            "Subscriber {Subscriber} -> START | EventType={EventType}, EventId={EventId}, ClientId={ClientId}",
            Name,
            message.EventType,
            message.EventId,
            message.ClientId
        );

        try
        {
            if (!_isAvailable)
                throw new InvalidOperationException($"Subscriber {Name} is UNAVAILABLE.");

            await Task.Delay(processingDelay);

            if (ConsumeFailure())
                throw new InvalidOperationException(
                    $"Subscriber {Name} simulated a processing failure."
                );

            await ProcessAsync(message);

            lock (_sync)
            {
                _receivedEvents.Enqueue(message);
                while (_receivedEvents.Count > MaximumStoredEvents)
                    _receivedEvents.Dequeue();
            }

            logger.LogInformation(
                "Subscriber {Subscriber} -> SUCCESS | EventType={EventType}, EventId={EventId}, ClientId={ClientId}, DurationMs={DurationMs}",
                Name,
                message.EventType,
                message.EventId,
                message.ClientId,
                (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds
            );
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Subscriber {Subscriber} -> FAILED | EventType={EventType}, EventId={EventId}, ClientId={ClientId}, DurationMs={DurationMs}",
                Name,
                message.EventType,
                message.EventId,
                message.ClientId,
                (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds
            );
            throw;
        }
    }

    public void SetAvailable(bool available)
    {
        _isAvailable = available;
        logger.LogWarning(
            "Subscriber {Subscriber} availability changed: {State}",
            Name,
            State
        );
    }

    public void FailNextDeliveries(int attempts)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(attempts);
        Interlocked.Exchange(ref _failuresRemaining, attempts);
        logger.LogWarning(
            "Subscriber {Subscriber} will fail the next {Attempts} processing attempts",
            Name,
            attempts
        );
    }

    public IReadOnlyList<DomainEvent> GetReceivedEvents()
    {
        lock (_sync)
            return _receivedEvents.ToArray();
    }

    protected abstract Task ProcessAsync(DomainEvent message);

    private bool ConsumeFailure()
    {
        while (true)
        {
            var remaining = Volatile.Read(ref _failuresRemaining);
            if (remaining <= 0)
                return false;

            if (Interlocked.CompareExchange(ref _failuresRemaining, remaining - 1, remaining) == remaining)
                return true;
        }
    }
}

public sealed class EventLogSubscriber(ILogger<EventLogSubscriber> logger)
    : EventSubscriberBase(
        "Logger",
        DomainEventTypes.All,
        TimeSpan.FromSeconds(1),
        logger
    )
{
    protected override Task ProcessAsync(DomainEvent message) => Task.CompletedTask;
}

public sealed class BookingStateSubscriber(ILogger<BookingStateSubscriber> logger)
    : EventSubscriberBase(
        "BusinessService",
        [
            DomainEventTypes.BookingCreated,
            DomainEventTypes.RoomReserved,
            DomainEventTypes.PaymentCompleted,
            DomainEventTypes.BookingCancelled,
        ],
        TimeSpan.FromSeconds(3),
        logger
    )
{
    private readonly object _stateLock = new();
    private readonly Dictionary<Guid, string> _bookingStates = new();
    private readonly Dictionary<Guid, string> _reservedRooms = new();

    public IReadOnlyDictionary<Guid, string> GetBookingStates()
    {
        lock (_stateLock)
            return new Dictionary<Guid, string>(_bookingStates);
    }

    public IReadOnlyDictionary<Guid, string> GetReservedRooms()
    {
        lock (_stateLock)
            return new Dictionary<Guid, string>(_reservedRooms);
    }

    protected override Task ProcessAsync(DomainEvent message)
    {
        lock (_stateLock)
        {
            switch (message.EventType)
            {
                case DomainEventTypes.BookingCreated:
                    _bookingStates[message.EntityId] = "Created";
                    break;
                case DomainEventTypes.PaymentCompleted:
                    if (TryReadGuid(message.Data, "BookingId", out var paidBookingId))
                        _bookingStates[paidBookingId] = "Paid";
                    break;
                case DomainEventTypes.BookingCancelled:
                    _bookingStates[message.EntityId] = "Cancelled";
                    break;
                case DomainEventTypes.RoomReserved:
                    _reservedRooms[message.EntityId] = "Reserved";
                    break;
            }
        }

        return Task.CompletedTask;
    }

    private static bool TryReadGuid(
        System.Text.Json.JsonElement data,
        string propertyName,
        out Guid value
    )
    {
        foreach (var property in data.EnumerateObject())
        {
            if (
                string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == System.Text.Json.JsonValueKind.String
                && Guid.TryParse(property.Value.GetString(), out value)
            )
                return true;
        }

        value = Guid.Empty;
        return false;
    }
}

public sealed class NotificationSubscriber(ILogger<NotificationSubscriber> logger)
    : EventSubscriberBase(
        "NotificationService",
        [
            DomainEventTypes.GuestCreated,
            DomainEventTypes.BookingCreated,
            DomainEventTypes.PaymentCompleted,
            DomainEventTypes.BookingCancelled,
        ],
        TimeSpan.FromSeconds(2),
        logger
    )
{
    private readonly object _notificationLock = new();
    private readonly List<string> _notifications = new();

    public IReadOnlyList<string> GetNotifications()
    {
        lock (_notificationLock)
            return _notifications.ToArray();
    }

    protected override Task ProcessAsync(DomainEvent message)
    {
        lock (_notificationLock)
        {
            _notifications.Add(
                $"{message.EventType} for {message.EntityId} (client {message.ClientId})"
            );
        }

        return Task.CompletedTask;
    }
}

public sealed class AnalyticsSubscriber(ILogger<AnalyticsSubscriber> logger)
    : EventSubscriberBase(
        "AnalyticsService",
        DomainEventTypes.All,
        TimeSpan.FromMilliseconds(500),
        logger
    )
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _eventCounts =
        new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> GetStatistics() =>
        new Dictionary<string, int>(_eventCounts, StringComparer.Ordinal);

    protected override Task ProcessAsync(DomainEvent message)
    {
        _eventCounts.AddOrUpdate(message.EventType, 1, (_, count) => count + 1);
        return Task.CompletedTask;
    }
}

public sealed class RuntimeEventSubscriber(
    string name,
    IReadOnlyCollection<string> eventTypes,
    ILogger<RuntimeEventSubscriber> logger
) : EventSubscriberBase(name, eventTypes, TimeSpan.Zero, logger)
{
    protected override Task ProcessAsync(DomainEvent message) => Task.CompletedTask;
}
