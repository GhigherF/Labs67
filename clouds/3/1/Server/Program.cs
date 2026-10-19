using System.Collections.Concurrent;
using HotelBooking;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<BookingService>();

var app = builder.Build();

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
            // Создаём новый идентификатор клиента
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

        // Имя клиента
        var clientName = $"Client-{clientId}";

        // Регистрируем клиента
        if (connectedClients.TryAdd(clientId, clientName))
        {
            isNewClient = true;
        }

        // Сохраняем данные клиента в HttpContext
        context.Items["ClientId"] = clientId;
        context.Items["ClientName"] = clientName;

        // ============================================================
        // 2. Информация о клиенте и операции
        // ============================================================

        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";

        var userAgent = context.Request.Headers.UserAgent.ToString();

        var method = context.Request.Method;
        var path = context.Request.Path;

        // Название выполняемой операции
        var operation = $"{method} {path}";

        // ============================================================
        // 3. Логирование подключения нового клиента
        // ============================================================

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

        // ============================================================
        // 4. Создание отдельной Task для обработки клиента
        // ============================================================

        var clientTask = Task.Run(async () =>
        {
            // ----------------------------------------------------
            // START
            // ----------------------------------------------------

            logger.LogInformation(
                "[{Time:HH:mm:ss.fff}] {ClientName} → START | Operation: {Operation} | Task: {TaskId} | Thread: {ThreadId}",
                DateTime.Now,
                clientName,
                operation,
                Task.CurrentId?.ToString() ?? "N/A",
                Environment.CurrentManagedThreadId
            );

            try
            {
                await Task.Delay(10_000);
                await next(context);
            }
            finally
            {
                // ------------------------------------------------
                // END
                // ------------------------------------------------

                logger.LogInformation(
                    "[{Time:HH:mm:ss.fff}] {ClientName} → END | Operation: {Operation} | Task: {TaskId} | Thread: {ThreadId} | Status: {StatusCode}",
                    DateTime.Now,
                    clientName,
                    operation,
                    Task.CurrentId?.ToString() ?? "N/A",
                    Environment.CurrentManagedThreadId,
                    context.Response.StatusCode
                );
            }
        });

        // Ждём завершения именно этой Task.
        // Другие HTTP-запросы при этом могут обрабатываться
        // своими Task параллельно.
        await clientTask;
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
                clientId = clientId,
            }
        );
    }
);

app.MapGet(
    "/api/rooms",
    ([FromServices] BookingService service, HttpContext context, ILogger<Program> logger) =>
    {
        var clientName = context.Items["ClientName"]?.ToString() ?? "Unknown-Client";

        logger.LogInformation("[{ClientName}] Запрос списка комнат.", clientName);

        return Results.Ok(service.Data.Rooms);
    }
);

app.MapGet(
    "/api/guests",
    ([FromServices] BookingService service, HttpContext context, ILogger<Program> logger) =>
    {
        var clientName = context.Items["ClientName"]?.ToString() ?? "Unknown-Client";

        logger.LogInformation("[{ClientName}] Запрос списка гостей.", clientName);

        return Results.Ok(service.Data.Guests);
    }
);

app.MapGet(
    "/api/bookings",
    ([FromServices] BookingService service, HttpContext context, ILogger<Program> logger) =>
    {
        var clientName = context.Items["ClientName"]?.ToString() ?? "Unknown-Client";

        logger.LogInformation("[{ClientName}] Запрос списка бронирований.", clientName);

        return Results.Ok(service.Data.Bookings);
    }
);

app.MapGet(
    "/api/payments",
    ([FromServices] BookingService service, HttpContext context, ILogger<Program> logger) =>
    {
        var clientName = context.Items["ClientName"]?.ToString() ?? "Unknown-Client";

        logger.LogInformation("[{ClientName}] Запрос списка платежей.", clientName);

        return Results.Ok(service.Data.Payments);
    }
);

app.MapGet(
    "/api/check-availability",
    (
        [FromServices] BookingService service,
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

        var roomExists = service.Data.Rooms.Any(r => r.Id == roomId);

        if (!roomExists)
        {
            logger.LogWarning(
                "[{ClientName}] Попытка проверки несуществующего номера {RoomId}",
                clientName,
                roomId
            );

            return Results.BadRequest(
                new { message = $"Номер с GUID '{roomId}' не найден в data.json." }
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
        [FromServices] BookingService service,
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
            logger.LogWarning("[{ClientName}] Попытка создания гостя с пустым именем.", clientName);

            return Results.BadRequest(new { message = "ФИО гостя не может быть пустым." });
        }

        try
        {
            var guest = new Guest { FullName = request.FullName };

            service.Data.Guests.Add(guest);

            service.SaveChanges();

            logger.LogInformation(
                "[{ClientName}] Успешно создан гость {GuestId} ({FullName})",
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
        [FromServices] BookingService service,
        HttpContext context,
        ILogger<Program> logger,
        CreateBookingRequest request
    ) =>
    {
        var clientName = context.Items["ClientName"]?.ToString() ?? "Unknown-Client";

        logger.LogInformation(
            "[{ClientName}] Попытка создания бронирования. GuestId: {GuestId}, RoomId: {RoomId}",
            clientName,
            request.GuestId,
            request.RoomId
        );

        try
        {
            var room = service.Data.Rooms.FirstOrDefault(r => r.Id == request.RoomId);

            if (room == null)
            {
                logger.LogWarning(
                    "[{ClientName}] Номер {RoomId} не найден при бронировании.",
                    clientName,
                    request.RoomId
                );

                return Results.BadRequest(
                    new
                    {
                        message = $"Ошибка: Номер с GUID '{request.RoomId}' отсутствует в базе data.json!",
                    }
                );
            }

            var (booking, createdEvent, reservedEvent) = service.CreateBooking(
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
                booking.Id
            );

            return Results.Created(
                $"/api/bookings/{booking.Id}",
                new
                {
                    Booking = booking,
                    CreatedEvent = createdEvent,
                    ReservedEvent = reservedEvent,
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
        [FromServices] BookingService service,
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
            var (payment, paymentEvent, change) = service.PayBooking(id, request.Amount);

            logger.LogInformation(
                "[{ClientName}] Оплата бронирования {BookingId} прошла успешно.",
                clientName,
                id
            );

            return Results.Ok(
                new
                {
                    Payment = payment,
                    PaymentEvent = paymentEvent,
                    Change = change > 0 ? change : (decimal?)null,
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
        [FromServices] BookingService service,
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
                "[{ClientName}] Бронирование {BookingId} успешно отменено.",
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
            "[{ClientName}] Обращение к несуществующему маршруту: {Path}",
            clientName,
            context.Request.Path
        );

        return Results.NotFound(new { message = "LoL, WRONG URL" });
    }
);

app.Run();

public record CreateGuestRequest(string FullName);

public record CreateBookingRequest(
    Guid GuestId,
    Guid RoomId,
    DateTime CheckInDate,
    DateTime CheckOutDate
);

public record PayBookingRequest(decimal Amount);

public record CancelBookingRequest(string Reason);
