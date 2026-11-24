namespace HotelBooking;

public sealed class EventSubscriberRegistry(
    EventBrokerCluster brokers,
    IEnumerable<IControllableEventSubscriber> defaultSubscribers,
    ILoggerFactory loggerFactory
)
{
    private readonly object _sync = new();
    private readonly Dictionary<string, SubscriberRegistration> _subscribers = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly IControllableEventSubscriber[] _defaultSubscribers =
        defaultSubscribers.ToArray();

    public void RegisterDefaults()
    {
        foreach (var subscriber in _defaultSubscribers)
            Register(subscriber, subscriber.EventTypes);
    }

    public SubscriberInfo RegisterRuntimeSubscriber(string name, IEnumerable<string> eventTypes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(eventTypes);

        var normalizedName = name.Trim();
        var normalizedTypes = ValidateEventTypes(eventTypes);
        var subscriber = new RuntimeEventSubscriber(
            normalizedName,
            normalizedTypes,
            loggerFactory.CreateLogger<RuntimeEventSubscriber>()
        );

        return Register(subscriber, normalizedTypes);
    }

    public IReadOnlyList<SubscriberInfo> GetSubscribers()
    {
        lock (_sync)
        {
            return _subscribers.Values
                .OrderBy(item => item.Subscriber.Name, StringComparer.OrdinalIgnoreCase)
                .Select(item => new SubscriberInfo(
                    item.Subscriber.Name,
                    item.Subscriber.IsAvailable ? "AVAILABLE" : "UNAVAILABLE",
                    item.EventTypes,
                    item.Subscriber.ReceivedCount
                ))
                .ToArray();
        }
    }

    public IReadOnlyList<SubscriberReceivedEvent> GetReceivedEvents()
    {
        lock (_sync)
        {
            return _subscribers.Values
                .SelectMany(registration =>
                    registration
                        .Subscriber.GetReceivedEvents()
                        .Select(message =>
                            new SubscriberReceivedEvent(registration.Subscriber.Name, message)
                        )
                )
                .OrderBy(item => item.Event.CreatedAt)
                .ToArray();
        }
    }

    public SubscriberInfo SetAvailability(string name, bool available)
    {
        var registration = FindSubscriber(name);
        registration.Subscriber.SetAvailable(available);
        return GetInfo(registration);
    }

    public SubscriberInfo FailNextDeliveries(string name, int attempts)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(attempts);
        var registration = FindSubscriber(name);
        registration.Subscriber.FailNextDeliveries(attempts);
        return GetInfo(registration);
    }

    public IReadOnlyList<EventSubscriptionInfo> GetSubscriptions() => brokers.GetSubscriptions();

    private SubscriberInfo Register(
        IControllableEventSubscriber subscriber,
        IReadOnlyCollection<string> eventTypes
    )
    {
        lock (_sync)
        {
            if (_subscribers.ContainsKey(subscriber.Name))
                throw new ArgumentException(
                    $"Subscriber already registered: {subscriber.Name}",
                    nameof(subscriber)
                );

            var normalizedTypes = ValidateEventTypes(eventTypes);
            foreach (var eventType in normalizedTypes)
                brokers.Subscribe(eventType, subscriber);

            var registration = new SubscriberRegistration(subscriber, normalizedTypes);
            _subscribers.Add(subscriber.Name, registration);
            return GetInfo(registration);
        }
    }

    private SubscriberRegistration FindSubscriber(string name)
    {
        lock (_sync)
        {
            if (!_subscribers.TryGetValue(name, out var registration))
                throw new ArgumentException($"Unknown subscriber: {name}", nameof(name));
            return registration;
        }
    }

    private static SubscriberInfo GetInfo(SubscriberRegistration registration) =>
        new(
            registration.Subscriber.Name,
            registration.Subscriber.IsAvailable ? "AVAILABLE" : "UNAVAILABLE",
            registration.EventTypes,
            registration.Subscriber.ReceivedCount
        );

    private static string[] ValidateEventTypes(IEnumerable<string> eventTypes)
    {
        var normalizedTypes = eventTypes
            .Where(eventType => !string.IsNullOrWhiteSpace(eventType))
            .Select(eventType => eventType.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedTypes.Length == 0)
            throw new ArgumentException("At least one event type is required.", nameof(eventTypes));

        var unknownType = normalizedTypes.FirstOrDefault(type =>
            !DomainEventTypes.All.Contains(type, StringComparer.Ordinal)
        );

        if (unknownType is not null)
            throw new ArgumentException($"Unsupported event type: {unknownType}", nameof(eventTypes));

        return normalizedTypes;
    }

    private sealed record SubscriberRegistration(
        IControllableEventSubscriber Subscriber,
        IReadOnlyList<string> EventTypes
    );
}
