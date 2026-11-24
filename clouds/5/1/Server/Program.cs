using System.Collections.Concurrent;
using HotelBooking;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

var databasePath = Path.Combine(builder.Environment.ContentRootPath, "hotel.db");

var connectionString = new SqliteConnectionStringBuilder
{
    DataSource = databasePath,
    ForeignKeys = true,
}.ToString();

builder.Configuration["ConnectionStrings:HotelDb"] = connectionString;

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<BookingService>();
builder.Services.AddSingleton<EventBroker>(provider =>
    new EventBroker("broker-1", provider.GetRequiredService<ILogger<EventBroker>>())
);
builder.Services.AddSingleton<EventBroker>(provider =>
    new EventBroker("broker-2", provider.GetRequiredService<ILogger<EventBroker>>())
);
builder.Services.AddSingleton<EventBrokerCluster>();
builder.Services.AddSingleton<IHostedService>(provider =>
    provider.GetRequiredService<EventBrokerCluster>()
);
builder.Services.AddSingleton<EventPublisher>();
builder.Services.AddSingleton<EventLogSubscriber>();
builder.Services.AddSingleton<BookingStateSubscriber>();
builder.Services.AddSingleton<NotificationSubscriber>();
builder.Services.AddSingleton<AnalyticsSubscriber>();
builder.Services.AddSingleton<IControllableEventSubscriber>(provider =>
    provider.GetRequiredService<EventLogSubscriber>()
);
builder.Services.AddSingleton<IControllableEventSubscriber>(provider =>
    provider.GetRequiredService<BookingStateSubscriber>()
);
builder.Services.AddSingleton<IControllableEventSubscriber>(provider =>
    provider.GetRequiredService<NotificationSubscriber>()
);
builder.Services.AddSingleton<IControllableEventSubscriber>(provider =>
    provider.GetRequiredService<AnalyticsSubscriber>()
);
builder.Services.AddSingleton<EventSubscriberRegistry>();

var app = builder.Build();

app.Services.GetRequiredService<EventSubscriberRegistry>().RegisterDefaults();

app.MapGet(
    "/api/events/subscriptions",
    (EventSubscriberRegistry subscribers) => Results.Ok(subscribers.GetSubscriptions())
);

app.MapGet(
    "/api/events/received",
    (EventSubscriberRegistry subscribers) => Results.Ok(subscribers.GetReceivedEvents())
);

app.MapGet(
    "/api/events/deliveries",
    (EventBrokerCluster brokers) => Results.Ok(brokers.GetDeliveryReports())
);

app.MapGet(
    "/api/events/deliveries/{eventId:guid}",
    (Guid eventId, EventBrokerCluster brokers) =>
    {
        var report = brokers.GetDeliveryReport(eventId);
        return report is null ? Results.NotFound(new { message = "Event not found." }) : Results.Ok(report);
    }
);

app.MapGet(
    "/api/brokers",
    (EventBrokerCluster brokers) => Results.Ok(brokers.GetBrokerStatuses())
);

app.MapPost(
    "/api/brokers/{brokerId}/availability",
    (string brokerId, AvailabilityRequest request, EventBrokerCluster brokers) =>
    {
        try
        {
            return Results.Ok(brokers.SetBrokerAvailability(brokerId, request.Available));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }
);

app.MapGet(
    "/api/subscribers",
    (EventSubscriberRegistry subscribers) => Results.Ok(subscribers.GetSubscribers())
);

app.MapGet(
    "/api/subscribers/{name}",
    (string name, EventSubscriberRegistry subscribers) =>
    {
        var subscriber = subscribers
            .GetSubscribers()
            .FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));

        return subscriber is null
            ? Results.NotFound(new { message = $"Subscriber not found: {name}" })
            : Results.Ok(subscriber);
    }
);

app.MapPost(
    "/api/subscribers",
    (RegisterSubscriberRequest request, EventSubscriberRegistry subscribers) =>
    {
        try
        {
            return Results.Created(
                $"/api/subscribers/{Uri.EscapeDataString(request.Name)}",
                subscribers.RegisterRuntimeSubscriber(request.Name, request.EventTypes)
            );
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }
);

app.MapPut(
    "/api/subscribers/{name}/availability",
    (string name, AvailabilityRequest request, EventSubscriberRegistry subscribers) =>
    {
        try
        {
            return Results.Ok(subscribers.SetAvailability(name, request.Available));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }
);

app.MapPost(
    "/api/subscribers/{name}/fail-next",
    (string name, FailNextDeliveriesRequest request, EventSubscriberRegistry subscribers) =>
    {
        try
        {
            return Results.Ok(subscribers.FailNextDeliveries(name, request.Attempts));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }
);

app.MapGet(
    "/api/analytics",
    (AnalyticsSubscriber analytics) => Results.Ok(analytics.GetStatistics())
);

app.MapGet(
    "/api/business/booking-states",
    (BookingStateSubscriber business) => Results.Ok(business.GetBookingStates())
);

app.MapGet(
    "/api/business/reserved-rooms",
    (BookingStateSubscriber business) => Results.Ok(business.GetReservedRooms())
);

app.MapGet(
    "/api/notifications",
    (NotificationSubscriber notifications) => Results.Ok(notifications.GetNotifications())
);

app.MapGet(
    "/api/database/status",
    (BookingService service) =>
    {
        return Results.Ok(service.GetDatabaseStatus());
    }
);

app.MapPost(
    "/api/database/primary",
    (AvailabilityRequest request, BookingService service) =>
    {
        try
        {
            service.SetPrimaryAvailable(request.Available);
            return Results.Ok(service.GetDatabaseStatus());
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
);

app.MapPost(
    "/api/database/replica",
    (AvailabilityRequest request, BookingService service) =>
    {
        try
        {
            service.SetReplicaAvailable(request.Available);
            return Results.Ok(service.GetDatabaseStatus());
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
);

await using (var connection = new SqliteConnection(connectionString))
{
    await connection.OpenAsync();

    app.Logger.LogInformation("SQLite connected: {DatabasePath}", databasePath);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

int nextClientId = 0;

var connectedClients = new ConcurrentDictionary<int, string>();

app.Use(
    async (context, next) =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

        const string clientCookieName = "ClientId";

        int clientId;
        bool isNewClient = false;

        if (
            context.Request.Cookies.TryGetValue(clientCookieName, out var clientIdValue)
            && int.TryParse(clientIdValue, out clientId)
            && clientId > 0
        ) { }
        else
        {
            clientId = Interlocked.Increment(ref nextClientId);
            isNewClient = true;

            context.Response.Cookies.Append(
                clientCookieName,
                clientId.ToString(),
                new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                }
            );
        }

        var clientName = $"Client-{clientId}";

        if (connectedClients.TryAdd(clientId, clientName))
        {
            isNewClient = true;
        }

        context.Items["ClientId"] = clientId;
        context.Items["ClientName"] = clientName;

        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";

        var userAgent = context.Request.Headers.UserAgent.ToString();

        var operation = $"{context.Request.Method} {context.Request.Path}";

        if (isNewClient)
        {
            logger.LogInformation("==================================================");

            logger.LogInformation(
                "[CLIENT CONNECTED] {ClientName} | Client ID: {ClientId} | IP: {ClientIp}",
                clientName,
                clientId,
                clientIp
            );

            logger.LogInformation("[CLIENT CONNECTED] User-Agent: {UserAgent}", userAgent);

            logger.LogInformation("==================================================");
        }

        logger.LogInformation(
            "[{Time:HH:mm:ss.fff}] {ClientName} -> START | Operation: {Operation} | Task: {TaskId} | Thread: {ThreadId}",
            DateTime.Now,
            clientName,
            operation,
            Task.CurrentId?.ToString() ?? "N/A",
            Environment.CurrentManagedThreadId
        );

        try
        {
            await Task.Delay(3_000);
            await next(context);
        }
        finally
        {
            logger.LogInformation(
                "[{Time:HH:mm:ss.fff}] {ClientName} -> END | Operation: {Operation} | Task: {TaskId} | Thread: {ThreadId} | Status: {StatusCode}",
                DateTime.Now,
                clientName,
                operation,
                Task.CurrentId?.ToString() ?? "N/A",
                Environment.CurrentManagedThreadId,
                context.Response.StatusCode
            );
        }
    }
);

app.MapGet(
    "/",
    (HttpContext context, ILogger<Program> logger) =>
    {
        var clientName = context.Items["ClientName"]?.ToString() ?? "Unknown-Client";

        var clientId = context.Items["ClientId"]?.ToString() ?? "Unknown";

        logger.LogInformation("[{ClientName}] Открыта главная страница.", clientName);

        return Results.Ok(
            new
            {
                message = "WOOOHOOO!!!",
                client = clientName,
                clientId,
            }
        );
    }
);

app.MapGet(
    "/api/rooms",
    (BookingService service, HttpContext context, ILogger<Program> logger) =>
    {
        var clientName = context.Items["ClientName"]?.ToString() ?? "Unknown-Client";

        logger.LogInformation("[{ClientName}] Запрос списка комнат.", clientName);

        return Results.Ok(service.Data.Rooms);
    }
);

app.MapGet(
    "/api/guests",
    (BookingService service, HttpContext context, ILogger<Program> logger) =>
    {
        var clientName = context.Items["ClientName"]?.ToString() ?? "Unknown-Client";

        logger.LogInformation("[{ClientName}] Запрос списка гостей.", clientName);

        return Results.Ok(service.Data.Guests);
    }
);

app.MapGet(
    "/api/bookings",
    (BookingService service, HttpContext context, ILogger<Program> logger) =>
    {
        var clientName = context.Items["ClientName"]?.ToString() ?? "Unknown-Client";

        logger.LogInformation("[{ClientName}] Запрос списка бронирований.", clientName);

        return Results.Ok(service.Data.Bookings);
    }
);

app.MapGet(
    "/api/payments",
    (BookingService service, HttpContext context, ILogger<Program> logger) =>
    {
        var clientName = context.Items["ClientName"]?.ToString() ?? "Unknown-Client";

        logger.LogInformation("[{ClientName}] Запрос списка платежей.", clientName);

        return Results.Ok(service.Data.Payments);
    }
);

app.MapGet(
    "/api/check-availability",
    (
        BookingService service,
        HttpContext context,
        ILogger<Program> logger,
        Guid roomId,
        DateTime checkIn,
        DateTime checkOut
    ) =>
    {
        var clientName = context.Items["ClientName"]?.ToString() ?? "Unknown-Client";

        logger.LogInformation(
            "[{ClientName}] Проверка доступности номера {RoomId} с {CheckIn} по {CheckOut}",
            clientName,
            roomId,
            checkIn,
            checkOut
        );

        var roomExists = service.Data.Rooms.Any(room => room.Id == roomId);

        if (!roomExists)
        {
            logger.LogWarning("[{ClientName}] Номер {RoomId} не найден.", clientName, roomId);

            return Results.BadRequest(
                new { message = $"Номер с GUID '{roomId}' не найден в базе данных." }
            );
        }

        var isAvailable = service.CheckAvailability(roomId, checkIn, checkOut);

        logger.LogInformation(
            "[{ClientName}] Результат проверки номера {RoomId}: доступен = {IsAvailable}",
            clientName,
            roomId,
            isAvailable
        );

        return Results.Ok(new { roomId, isAvailable });
    }
);

app.MapPost(
    "/api/guests",
    async (
        BookingService service,
        EventPublisher publisher,
        HttpContext context,
        ILogger<Program> logger,
        CreateGuestRequest request
    ) =>
    {
        var clientName = context.Items["ClientName"]?.ToString() ?? "Unknown-Client";

        logger.LogInformation(
            "[{ClientName}] Попытка создания гостя: {FullName}",
            clientName,
            request.FullName
        );

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            logger.LogWarning("[{ClientName}] Имя гостя пустое.", clientName);

            return Results.BadRequest(new { message = "ФИО гостя не может быть пустым." });
        }

        try
        {
            // Новый BookingService записывает гостя непосредственно в SQLite.
            var guest = service.AddGuest(request.FullName.Trim(), request.Email?.Trim() ?? "");
            var clientId = context.Items["ClientId"] is int currentClientId ? currentClientId : 0;
            var publishResult = await publisher.PublishAsync(
                DomainEvent.Create(
                    DomainEventTypes.GuestCreated,
                    guest.Id,
                    clientId,
                    new { guest.FullName, guest.Email }
                )
            );
            context.Response.Headers["X-Event-Ids"] = publishResult.EventId.ToString();

            logger.LogInformation(
                "[{ClientName}] Создан гость {GuestId} ({FullName})",
                clientName,
                guest.Id,
                guest.FullName
            );

            return Results.Created($"/api/guests/{guest.Id}", guest);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{ClientName}] Ошибка при создании гостя.", clientName);

            return Results.BadRequest(new { message = ex.Message });
        }
    }
);

app.MapPost(
    "/api/bookings",
    async (
        BookingService service,
        EventPublisher publisher,
        HttpContext context,
        ILogger<Program> logger,
        CreateBookingRequest request
    ) =>
    {
        var clientName = context.Items["ClientName"]?.ToString() ?? "Unknown-Client";

        logger.LogInformation(
            "[{ClientName}] Создание бронирования. GuestId: {GuestId}, RoomId: {RoomId}",
            clientName,
            request.GuestId,
            request.RoomId
        );

        try
        {
            var room = service.Data.Rooms.FirstOrDefault(r => r.Id == request.RoomId);

            if (room is null)
            {
                logger.LogWarning(
                    "[{ClientName}] Номер {RoomId} не найден.",
                    clientName,
                    request.RoomId
                );

                return Results.BadRequest(
                    new { message = $"Номер с GUID '{request.RoomId}' отсутствует в базе данных." }
                );
            }

            var result = service.CreateBooking(
                request.GuestId,
                room.Id,
                request.CheckInDate,
                request.CheckOutDate,
                room.PricePerNight,
                clientName
            );

            var clientId = context.Items["ClientId"] is int currentClientId ? currentClientId : 0;
            var bookingPublication = await publisher.PublishAsync(
                DomainEvent.Create(
                    DomainEventTypes.BookingCreated,
                    result.booking.Id,
                    clientId,
                    result.createdEvent
                )
            );
            var reservationPublication = await publisher.PublishAsync(
                DomainEvent.Create(
                    DomainEventTypes.RoomReserved,
                    result.booking.RoomId,
                    clientId,
                    result.reservedEvent
                )
            );
            context.Response.Headers["X-Event-Ids"] =
                $"{bookingPublication.EventId},{reservationPublication.EventId}";

            logger.LogInformation(
                "[{ClientName}] Бронирование {BookingId} успешно создано.",
                clientName,
                result.booking.Id
            );

            return Results.Created(
                $"/api/bookings/{result.booking.Id}",
                new
                {
                    Booking = result.booking,
                    CreatedEvent = result.createdEvent,
                    ReservedEvent = result.reservedEvent,
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{ClientName}] Ошибка при создании бронирования.", clientName);

            return Results.BadRequest(new { message = ex.Message });
        }
    }
);

app.MapPost(
    "/api/bookings/{id:guid}/pay",
    async (
        BookingService service,
        EventPublisher publisher,
        HttpContext context,
        ILogger<Program> logger,
        Guid id,
        PayBookingRequest request
    ) =>
    {
        var clientName = context.Items["ClientName"]?.ToString() ?? "Unknown-Client";

        logger.LogInformation(
            "[{ClientName}] Оплата бронирования {BookingId} на сумму {Amount}",
            clientName,
            id,
            request.Amount
        );

        try
        {
            var result = service.PayBooking(id, request.Amount);
            var clientId = context.Items["ClientId"] is int currentClientId ? currentClientId : 0;
            var publishResult = await publisher.PublishAsync(
                DomainEvent.Create(
                    DomainEventTypes.PaymentCompleted,
                    result.payment.Id,
                    clientId,
                    result.paymentEvent
                )
            );
            context.Response.Headers["X-Event-Ids"] = publishResult.EventId.ToString();

            logger.LogInformation(
                "[{ClientName}] Оплата бронирования {BookingId} прошла успешно.",
                clientName,
                id
            );

            return Results.Ok(
                new
                {
                    Payment = result.payment,
                    PaymentEvent = result.paymentEvent,
                    Change = result.change > 0 ? result.change : (decimal?)null,
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "[{ClientName}] Ошибка при оплате бронирования {BookingId}.",
                clientName,
                id
            );

            return Results.BadRequest(new { message = ex.Message });
        }
    }
);

app.MapPost(
    "/api/bookings/{id:guid}/cancel",
    async (
        BookingService service,
        EventPublisher publisher,
        HttpContext context,
        ILogger<Program> logger,
        Guid id,
        CancelBookingRequest request
    ) =>
    {
        var clientName = context.Items["ClientName"]?.ToString() ?? "Unknown-Client";

        logger.LogInformation(
            "[{ClientName}] Отмена бронирования {BookingId}. Причина: {Reason}",
            clientName,
            id,
            request.Reason
        );

        try
        {
            var cancelEvent = service.CancelBooking(id, request.Reason);
            var clientId = context.Items["ClientId"] is int currentClientId ? currentClientId : 0;
            var publishResult = await publisher.PublishAsync(
                DomainEvent.Create(
                    DomainEventTypes.BookingCancelled,
                    cancelEvent.BookingId,
                    clientId,
                    cancelEvent
                )
            );
            context.Response.Headers["X-Event-Ids"] = publishResult.EventId.ToString();

            logger.LogInformation(
                "[{ClientName}] Бронирование {BookingId} отменено.",
                clientName,
                id
            );

            return Results.Ok(new { CancelledEvent = cancelEvent });
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "[{ClientName}] Ошибка при отмене бронирования {BookingId}.",
                clientName,
                id
            );

            return Results.BadRequest(new { message = ex.Message });
        }
    }
);

app.MapFallback(
    (HttpContext context) =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

        var clientName = context.Items["ClientName"]?.ToString() ?? "Unknown-Client";

        logger.LogWarning(
            "[{ClientName}] Несуществующий маршрут: {Path}",
            clientName,
            context.Request.Path
        );

        return Results.NotFound(new { message = "LoL, WRONG URL" });
    }
);

app.Run();

public record AvailabilityRequest(bool Available);

public record CreateGuestRequest(string FullName, string? Email = "");
