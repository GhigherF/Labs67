using System.Collections.Concurrent;
using HotelBooking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Server.Tests;

public sealed class PubSubTests
{
    private static readonly ILogger<EventBroker> BrokerLogger =
        NullLogger<EventBroker>.Instance;

    [Fact]
    public void DomainEvent_CreateSetsIdentityTimeAndPayload()
    {
        var entityId = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow;

        var message = DomainEvent.Create(
            DomainEventTypes.BookingCreated,
            entityId,
            clientId: 7,
            new { Guest = "Test guest" }
        );

        Assert.NotEqual(Guid.Empty, message.EventId);
        Assert.Equal(DomainEventTypes.BookingCreated, message.EventType);
        Assert.Equal(entityId, message.EntityId);
        Assert.Equal(7, message.ClientId);
        Assert.InRange(message.CreatedAt, before, DateTimeOffset.UtcNow);
        Assert.Equal("Test guest", message.Data.GetProperty("Guest").GetString());
    }

    [Fact]
    public async Task BrokerDeliversOnlyToSubscribersOfThePublishedType()
    {
        using var broker = CreateBroker("broker-test");
        var bookingSubscriber = new TestSubscriber(
            "BookingSubscriber",
            [DomainEventTypes.BookingCreated]
        );
        var paymentSubscriber = new TestSubscriber(
            "PaymentSubscriber",
            [DomainEventTypes.PaymentCompleted]
        );

        Assert.True(broker.Subscribe(DomainEventTypes.BookingCreated, bookingSubscriber));
        Assert.True(broker.Subscribe(DomainEventTypes.PaymentCompleted, paymentSubscriber));
        await broker.StartAsync(CancellationToken.None);

        var message = CreateEvent(DomainEventTypes.BookingCreated);
        var result = await broker.PublishAsync(message);
        await WaitUntilAsync(() => bookingSubscriber.ReceivedCount == 1);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.SubscriberCount);
        Assert.Equal(1, bookingSubscriber.ReceivedCount);
        Assert.Empty(paymentSubscriber.Received);
        Assert.Equal("DELIVERED", broker.GetDeliveryReport(message.EventId)?.Status);

        await broker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task BrokerFansOutOneEventToEveryMatchingSubscriber()
    {
        using var broker = CreateBroker("broker-test");
        var first = new TestSubscriber("First", [DomainEventTypes.GuestCreated]);
        var second = new TestSubscriber("Second", [DomainEventTypes.GuestCreated]);

        broker.Subscribe(DomainEventTypes.GuestCreated, first);
        broker.Subscribe(DomainEventTypes.GuestCreated, second);
        await broker.StartAsync(CancellationToken.None);

        var message = CreateEvent(DomainEventTypes.GuestCreated);
        var result = await broker.PublishAsync(message);
        await WaitUntilAsync(() => first.ReceivedCount == 1 && second.ReceivedCount == 1);

        Assert.Equal(2, result.SubscriberCount);
        Assert.Contains(message, first.Received);
        Assert.Contains(message, second.Received);

        await broker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task BrokerStoresEventsWithoutSubscribersAndRejectsUnknownTypes()
    {
        using var broker = CreateBroker("broker-test");
        await broker.StartAsync(CancellationToken.None);

        var noSubscriberEvent = CreateEvent(DomainEventTypes.RoomReserved);
        var result = await broker.PublishAsync(noSubscriberEvent);

        Assert.True(result.Accepted);
        Assert.Equal(0, result.SubscriberCount);
        Assert.Equal("NO_SUBSCRIBERS", broker.GetDeliveryReport(noSubscriberEvent.EventId)?.Status);

        var invalidEvent = CreateEvent("UnknownEvent");
        await Assert.ThrowsAsync<ArgumentException>(() => broker.PublishAsync(invalidEvent));

        await broker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task BrokerProcessesSubscribersIndependently()
    {
        using var broker = CreateBroker("broker-test", workerCount: 2);
        var slowSubscriber = new TestSubscriber(
            "Slow",
            [DomainEventTypes.GuestCreated],
            processingDelay: TimeSpan.FromMilliseconds(300)
        );
        var fastSubscriber = new TestSubscriber(
            "Fast",
            [DomainEventTypes.GuestCreated],
            processingDelay: TimeSpan.FromMilliseconds(10)
        );

        broker.Subscribe(DomainEventTypes.GuestCreated, slowSubscriber);
        broker.Subscribe(DomainEventTypes.GuestCreated, fastSubscriber);
        await broker.StartAsync(CancellationToken.None);

        await broker.PublishAsync(CreateEvent(DomainEventTypes.GuestCreated));
        await WaitUntilAsync(() => fastSubscriber.ReceivedCount == 1);

        Assert.Equal(0, slowSubscriber.ReceivedCount);
        await WaitUntilAsync(() => slowSubscriber.ReceivedCount == 1);

        await broker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task BrokerKeepsClientCorrelationForConcurrentPublications()
    {
        using var broker = CreateBroker("broker-test", workerCount: 4);
        var subscriber = new TestSubscriber("Logger", [DomainEventTypes.GuestCreated]);
        broker.Subscribe(DomainEventTypes.GuestCreated, subscriber);
        await broker.StartAsync(CancellationToken.None);

        var messages = Enumerable
            .Range(1, 3)
            .Select(clientId => CreateEvent(DomainEventTypes.GuestCreated, clientId))
            .ToArray();

        await Task.WhenAll(messages.Select(message => broker.PublishAsync(message)));
        await WaitUntilAsync(() => subscriber.ReceivedCount == 3);

        Assert.Equal(
            new[] { 1, 2, 3 },
            subscriber.Received.Select(message => message.ClientId).Order()
        );

        await broker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task BrokerRetriesFailedSubscriberWithoutBlockingOtherSubscribers()
    {
        using var broker = CreateBroker("broker-test");
        var retryingSubscriber = new TestSubscriber(
            "Retrying",
            [DomainEventTypes.BookingCreated],
            failuresBeforeSuccess: 2
        );
        var healthySubscriber = new TestSubscriber("Healthy", [DomainEventTypes.BookingCreated]);

        broker.Subscribe(DomainEventTypes.BookingCreated, retryingSubscriber);
        broker.Subscribe(DomainEventTypes.BookingCreated, healthySubscriber);
        await broker.StartAsync(CancellationToken.None);

        var message = CreateEvent(DomainEventTypes.BookingCreated);
        await broker.PublishAsync(message);
        await WaitUntilAsync(
            () =>
                retryingSubscriber.ReceivedCount == 1
                && healthySubscriber.ReceivedCount == 1
        );

        var report = broker.GetDeliveryReport(message.EventId);
        Assert.NotNull(report);
        Assert.Equal("DELIVERED", report.Status);
        Assert.Equal(3, Assert.Single(report.Deliveries, item => item.SubscriberName == "Retrying").Attempts);
        Assert.Equal(1, Assert.Single(report.Deliveries, item => item.SubscriberName == "Healthy").Attempts);

        await broker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task BrokerContinuesOtherDeliveriesWhenOneSubscriberFailsPermanently()
    {
        using var broker = CreateBroker("broker-test");
        var failedSubscriber = new TestSubscriber(
            "Failing",
            [DomainEventTypes.GuestCreated],
            alwaysFail: true
        );
        var healthySubscriber = new TestSubscriber("Healthy", [DomainEventTypes.GuestCreated]);

        broker.Subscribe(DomainEventTypes.GuestCreated, failedSubscriber);
        broker.Subscribe(DomainEventTypes.GuestCreated, healthySubscriber);
        await broker.StartAsync(CancellationToken.None);

        var message = CreateEvent(DomainEventTypes.GuestCreated);
        await broker.PublishAsync(message);
        await WaitUntilAsync(
            () =>
                broker.GetDeliveryReport(message.EventId)?.Status == "PARTIAL"
                && healthySubscriber.ReceivedCount == 1
        );

        var report = broker.GetDeliveryReport(message.EventId);
        Assert.NotNull(report);
        Assert.Equal("FAILED", Assert.Single(report.Deliveries, item => item.SubscriberName == "Failing").Status);
        Assert.Equal("DELIVERED", Assert.Single(report.Deliveries, item => item.SubscriberName == "Healthy").Status);
        Assert.Equal(3, failedSubscriber.AttemptCount);

        await broker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task PublisherWorksWithSubscriberRegisteredAfterPublisherCreation()
    {
        using var brokers = CreateBrokerCluster();
        var publisher = new EventPublisher(brokers, NullLogger<EventPublisher>.Instance);
        await brokers.StartAsync(CancellationToken.None);

        var lateSubscriber = new TestSubscriber("LateSubscriber", [DomainEventTypes.GuestCreated]);
        brokers.Subscribe(DomainEventTypes.GuestCreated, lateSubscriber);

        var message = CreateEvent(DomainEventTypes.GuestCreated);
        var result = await publisher.PublishAsync(message);
        await WaitUntilAsync(() => lateSubscriber.ReceivedCount == 1);

        Assert.True(result.Accepted);
        Assert.Equal(1, result.SubscriberCount);
        Assert.Contains(message, lateSubscriber.Received);

        await brokers.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task PublisherUsesSecondBrokerWhenFirstIsUnavailable()
    {
        using var brokers = CreateBrokerCluster();
        var subscriber = new TestSubscriber("Subscriber", [DomainEventTypes.GuestCreated]);
        brokers.Subscribe(DomainEventTypes.GuestCreated, subscriber);
        await brokers.StartAsync(CancellationToken.None);
        brokers.SetBrokerAvailability("broker-1", false);

        var result = await new EventPublisher(
                brokers,
                NullLogger<EventPublisher>.Instance
            )
            .PublishAsync(CreateEvent(DomainEventTypes.GuestCreated));

        Assert.True(result.Accepted);
        Assert.Equal("broker-2", result.BrokerId);
        await WaitUntilAsync(() => subscriber.ReceivedCount == 1);

        await brokers.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task PublisherReturnsFailureWhenAllBrokersAreUnavailable()
    {
        using var brokers = CreateBrokerCluster();
        await brokers.StartAsync(CancellationToken.None);
        brokers.SetBrokerAvailability("broker-1", false);
        brokers.SetBrokerAvailability("broker-2", false);

        var result = await new EventPublisher(
                brokers,
                NullLogger<EventPublisher>.Instance
            )
            .PublishAsync(CreateEvent(DomainEventTypes.GuestCreated));

        Assert.False(result.Accepted);
        Assert.Null(result.BrokerId);
        Assert.Contains("No available brokers", result.Error);

        await brokers.StopAsync(CancellationToken.None);
    }

    private static EventBroker CreateBroker(string id, int workerCount = 4) =>
        new(
            id,
            BrokerLogger,
            workerCount: workerCount,
            maximumAttempts: 3,
            retryDelay: TimeSpan.FromMilliseconds(5)
        );

    private static EventBrokerCluster CreateBrokerCluster() =>
        new(
            [CreateBroker("broker-1"), CreateBroker("broker-2")],
            NullLogger<EventBrokerCluster>.Instance
        );

    private static DomainEvent CreateEvent(string eventType, int clientId = 1) =>
        DomainEvent.Create(eventType, Guid.NewGuid(), clientId, new { Value = "test" });

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow >= deadline)
                throw new TimeoutException("Timed out waiting for event delivery.");
            await Task.Delay(10);
        }
    }

    private sealed class TestSubscriber(
        string name,
        IReadOnlyCollection<string> eventTypes,
        int failuresBeforeSuccess = 0,
        bool alwaysFail = false,
        TimeSpan? processingDelay = null
    ) : IEventSubscriber
    {
        private readonly ConcurrentQueue<DomainEvent> _received = new();
        private int _attemptCount;
        private int _failuresRemaining = failuresBeforeSuccess;

        public string Name { get; } = name;
        public IReadOnlyCollection<string> EventTypes { get; } = eventTypes;
        public int ReceivedCount => _received.Count;
        public int AttemptCount => Volatile.Read(ref _attemptCount);
        public IReadOnlyCollection<DomainEvent> Received => _received.ToArray();

        public async Task HandleAsync(DomainEvent message)
        {
            Interlocked.Increment(ref _attemptCount);

            if (processingDelay is not null)
                await Task.Delay(processingDelay.Value);

            if (alwaysFail)
                throw new InvalidOperationException("Simulated subscriber failure.");

            if (Interlocked.CompareExchange(ref _failuresRemaining, 0, 0) > 0)
            {
                Interlocked.Decrement(ref _failuresRemaining);
                throw new InvalidOperationException("Simulated temporary failure.");
            }

            _received.Enqueue(message);
        }
    }
}
