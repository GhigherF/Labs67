using System.Text.Json;
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


app.MapGet("/", () => "WOOOHOOO!!!");

app.MapGet("/api/rooms", ([FromServices] BookingService service) => Results.Ok(service.Data.Rooms));

app.MapGet(
    "/api/guests",
    ([FromServices] BookingService service) => Results.Ok(service.Data.Guests)
);

app.MapGet(
    "/api/bookings",
    ([FromServices] BookingService service) => Results.Ok(service.Data.Bookings)
);

app.MapGet(
    "/api/payments",
    ([FromServices] BookingService service) => Results.Ok(service.Data.Payments)
);

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


app.MapPost(
    "/api/guests",
    ([FromServices] BookingService service, CreateGuestRequest request) =>
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return Results.BadRequest(new { message = "ФИО гостя не может быть пустым." });
        }

        try
        {
            var guest = new Guest { FullName = request.FullName };

            service.Data.Guests.Add(guest);
            service.SaveChanges();

            return Results.Created($"/api/guests/{guest.Id}", guest);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }
);

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

app.MapPost(
    "/api/bookings/{id:guid}/pay",
    ([FromServices] BookingService service, Guid id, PayBookingRequest request) =>
    {
        try
        {
            var (payment, paymentEvent, change) = service.PayBooking(id, request.Amount);

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
            return Results.BadRequest(new { message = ex.Message });
        }
    }
);

app.MapPost(
    "/api/bookings/{id:guid}/cancel",
    ([FromServices] BookingService service, Guid id, CancelBookingRequest request) =>
    {
        try
        {
            var cancelEvent = service.CancelBooking(id, request.Reason);
            return Results.Ok(new { CancelledEvent = cancelEvent });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }
);

app.MapFallback(() => Results.NotFound(new { message = "LoL, WRONG URL" }));

app.Run();

public record CreateGuestRequest(string FullName);
