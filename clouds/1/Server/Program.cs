using HotelBooking;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<BookingService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/", () => "WOOOHOOO!!!");

// Получить список комнат
app.MapGet(
    "/api/rooms",
    ([FromServices] BookingService service) =>
    {
        return Results.Ok(service.Data.Rooms);
    }
);

// Проверка доступности
app.MapGet(
    "/api/check-availability",
    ([FromServices] BookingService service, Guid roomId, DateTime checkIn, DateTime checkOut) =>
    {
        var roomExists = service.Data.Rooms.Any(r => r.Id == roomId);
        if (!roomExists)
        {
            return Results.BadRequest(
                new { message = $"Номер с GUID '{roomId}' не найден в data.json." }
            );
        }

        bool isAvailable = service.CheckAvailability(roomId, checkIn, checkOut);
        return Results.Ok(new { roomId, isAvailable });
    }
);

// Создание бронирования
app.MapPost(
    "/api/bookings",
    ([FromServices] BookingService service, CreateBookingRequest request) =>
    {
        try
        {
            var room = service.Data.Rooms.FirstOrDefault(r => r.Id == request.RoomId);
            if (room == null)
            {
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
                room.PricePerNight
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
            return Results.BadRequest(new { message = ex.Message });
        }
    }
);

app.MapFallback(() => Results.NotFound(new { message = "LoL, WRONG URL" }));

app.Run();

public record CreateBookingRequest(
    Guid GuestId,
    Guid RoomId,
    DateTime CheckInDate,
    DateTime CheckOutDate
);
