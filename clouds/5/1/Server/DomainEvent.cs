using System.Text.Json;

namespace HotelBooking;

public static class DomainEventTypes
{
    public const string GuestCreated = "GuestCreated";
    public const string BookingCreated = "BookingCreated";
    public const string RoomReserved = "RoomReserved";
    public const string PaymentCompleted = "PaymentCompleted";
    public const string BookingCancelled = "BookingCancelled";

    public static IReadOnlyList<string> All { get; } =
    [
        GuestCreated,
        BookingCreated,
        RoomReserved,
        PaymentCompleted,
        BookingCancelled,
    ];
}

public sealed record DomainEvent(
    Guid EventId,
    string EventType,
    Guid EntityId,
    int ClientId,
    DateTimeOffset CreatedAt,
    JsonElement Data
)
{
    public static DomainEvent Create<T>(string eventType, Guid entityId, int clientId, T data) =>
        new(
            Guid.NewGuid(),
            eventType,
            entityId,
            clientId,
            DateTimeOffset.UtcNow,
            JsonSerializer.SerializeToElement(data)
        );
}

public sealed record EventPublishResult(
    Guid EventId,
    bool Accepted,
    string? BrokerId,
    int SubscriberCount,
    string? Error = null
);

public sealed record SubscriberDeliveryResult(
    Guid EventId,
    string EventType,
    Guid EntityId,
    int ClientId,
    string BrokerId,
    string SubscriberName,
    string Status,
    int Attempts,
    DateTimeOffset QueuedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    double? DurationMilliseconds,
    string? LastError
);

public sealed record EventDeliveryReport(
    DomainEvent Event,
    string BrokerId,
    DateTimeOffset QueuedAt,
    string Status,
    IReadOnlyList<SubscriberDeliveryResult> Deliveries
);

public sealed record BrokerStatus(string BrokerId, string State, int PendingDeliveries);

public sealed record EventSubscriptionInfo(string BrokerId, string EventType, IReadOnlyList<string> Subscribers);

public sealed record SubscriberReceivedEvent(string SubscriberName, DomainEvent Event);

public sealed record SubscriberInfo(
    string Name,
    string State,
    IReadOnlyList<string> EventTypes,
    int ReceivedCount
);

public sealed record AvailabilityRequest(bool Available);

public sealed record FailNextDeliveriesRequest(int Attempts);

public sealed record RegisterSubscriberRequest(string Name, string[] EventTypes);

public interface IEventSubscriber
{
    string Name { get; }

    IReadOnlyCollection<string> EventTypes { get; }

    Task HandleAsync(DomainEvent message);
}

public interface IControllableEventSubscriber : IEventSubscriber
{
    bool IsAvailable { get; }

    int ReceivedCount { get; }

    IReadOnlyList<DomainEvent> GetReceivedEvents();

    void SetAvailable(bool available);

    void FailNextDeliveries(int attempts);
}
