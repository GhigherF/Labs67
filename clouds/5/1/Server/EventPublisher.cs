namespace HotelBooking;

public sealed class EventPublisher(
    EventBrokerCluster brokers,
    ILogger<EventPublisher> logger
)
{
    public async Task<EventPublishResult> PublishAsync(DomainEvent message)
    {
        logger.LogInformation(
            "Publisher -> PUBLISH | EventType={EventType}, EntityId={EntityId}, EventId={EventId}, ClientId={ClientId}",
            message.EventType,
            message.EntityId,
            message.EventId,
            message.ClientId
        );

        try
        {
            var result = await brokers.PublishAsync(message);

            if (!result.Accepted)
            {
                logger.LogError(
                    "Event publication failed: EventId={EventId}, ClientId={ClientId}, Error={Error}",
                    result.EventId,
                    message.ClientId,
                    result.Error
                );
            }
            else if (result.SubscriberCount == 0)
            {
                logger.LogWarning(
                    "Event {EventId} was accepted by Broker {BrokerId} but had no subscribers",
                    result.EventId,
                    result.BrokerId
                );
            }
            else
            {
                logger.LogInformation(
                    "Event {EventId} accepted by Broker {BrokerId} for {SubscriberCount} subscribers",
                    result.EventId,
                    result.BrokerId,
                    result.SubscriberCount
                );
            }

            return result;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Event publication failed: EventId={EventId}, EventType={EventType}",
                message.EventId,
                message.EventType
            );

            return new EventPublishResult(
                message.EventId,
                false,
                null,
                0,
                exception.Message
            );
        }
    }
}
