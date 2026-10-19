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

// Используем одну и ту же SQLite-базу в Program.cs и BookingService.
builder.Configuration["ConnectionStrings:HotelDb"] = connectionString;

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<BookingService>();

var app = builder.Build();

// Проверяем подключение к SQLite.
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
    (
        BookingService service,
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
    (
        BookingService service,
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
    (
        BookingService service,
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
    (
        BookingService service,
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

public record CreateGuestRequest(string FullName, string? Email = "");
