using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace HotelBooking
{
    public record CreateBookingRequest(
        Guid GuestId,
        Guid RoomId,
        DateTime CheckInDate,
        DateTime CheckOutDate
    );

    public record PayBookingRequest(decimal Amount);

    public record CancelBookingRequest(string Reason);

    public enum BookingStatus
    {
        Created,
        Paid,
        Cancelled,
    }

    public class Hotel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    public class Room
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid HotelId { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public decimal PricePerNight { get; set; }
    }

    public class Guest
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class Booking
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid GuestId { get; set; }
        public Guid RoomId { get; set; }
        public DateTime CheckInDate { get; set; }
        public DateTime CheckOutDate { get; set; }
        public decimal TotalPrice { get; set; }
        public BookingStatus Status { get; set; } = BookingStatus.Created;
    }

    public class Payment
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid BookingId { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    }

    public class AppData
    {
        public List<Hotel> Hotels { get; set; } = new();
        public List<Room> Rooms { get; set; } = new();
        public List<Guest> Guests { get; set; } = new();
        public List<Booking> Bookings { get; set; } = new();
        public List<Payment> Payments { get; set; } = new();
    }

    public record BookingCreatedEvent(
        Guid BookingId,
        Guid GuestId,
        Guid RoomId,
        DateTime CheckInDate,
        DateTime CheckOutDate,
        decimal TotalPrice
    );

    public record RoomReservedEvent(
        Guid RoomId,
        Guid BookingId,
        DateTime CheckInDate,
        DateTime CheckOutDate
    );

    public record PaymentCompletedEvent(
        Guid PaymentId,
        Guid BookingId,
        decimal Amount,
        DateTime PaymentDate
    );

    public record BookingCancelledEvent(Guid BookingId, string Reason, DateTime CancelledAt);

    public class BookingService
    {
        private const string FilePath = "data.json";
        private readonly AppData _data;

        public BookingService()
        {
            _data = LoadFromFile();
        }

        public AppData Data => _data;

        private AppData LoadFromFile()
        {
            if (!File.Exists(FilePath))
                return new AppData();
            string json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<AppData>(json) ?? new AppData();
        }

        public void SaveChanges()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(_data, options);
            File.WriteAllText(FilePath, json);
        }

        public Guest AddGuest(string fullName, string email = "")
        {
            var guest = new Guest { FullName = fullName, Email = email };

            _data.Guests.Add(guest);
            SaveChanges(); // Сохраняет обновленный список в data.json

            return guest;
        }

        public bool CheckAvailability(Guid roomId, DateTime checkIn, DateTime checkOut)
        {
            return !_data.Bookings.Any(b =>
                b.RoomId == roomId
                && b.Status != BookingStatus.Cancelled
                && checkIn < b.CheckOutDate
                && checkOut > b.CheckInDate
            );
        }

        public (
            Booking booking,
            BookingCreatedEvent createdEvent,
            RoomReservedEvent reservedEvent
        ) CreateBooking(
            Guid guestId,
            Guid roomId,
            DateTime checkIn,
            DateTime checkOut,
            decimal pricePerNight
        )
        {
            if (!CheckAvailability(roomId, checkIn, checkOut))
            {
                throw new Exception("Номер занят на указанные даты.");
            }

            var days = (int)(checkOut - checkIn).TotalDays;
            var totalPrice = days * pricePerNight;

            var booking = new Booking
            {
                GuestId = guestId,
                RoomId = roomId,
                CheckInDate = checkIn,
                CheckOutDate = checkOut,
                TotalPrice = totalPrice,
                Status = BookingStatus.Created,
            };

            _data.Bookings.Add(booking);
            SaveChanges();

            var createdEvent = new BookingCreatedEvent(
                booking.Id,
                guestId,
                roomId,
                checkIn,
                checkOut,
                totalPrice
            );
            var reservedEvent = new RoomReservedEvent(roomId, booking.Id, checkIn, checkOut);

            return (booking, createdEvent, reservedEvent);
        }

        public (Payment payment, PaymentCompletedEvent paymentEvent, decimal change) PayBooking(
            Guid bookingId,
            decimal amount
        )
        {
            var booking =
                _data.Bookings.FirstOrDefault(b => b.Id == bookingId)
                ?? throw new Exception("Бронирование не найдено.");

            // Проверка на повторную оплату
            if (booking.Status == BookingStatus.Paid)
            {
                throw new Exception(
                    "Данное бронирование УЖЕ ОПЛАЧЕНО. Повторная оплата невозможна."
                );
            }

            if (booking.Status == BookingStatus.Cancelled)
            {
                throw new Exception("Нельзя оплатить отмененное бронирование.");
            }

            if (amount < booking.TotalPrice)
            {
                throw new Exception(
                    $"Недостаточно средств. К оплате: {booking.TotalPrice:C2}, внесено: {amount:C2}"
                );
            }

            // Расчёт сдачи
            decimal change = amount - booking.TotalPrice;

            booking.Status = BookingStatus.Paid;

            var payment = new Payment { BookingId = bookingId, Amount = amount };
            _data.Payments.Add(payment);
            SaveChanges();

            var paymentEvent = new PaymentCompletedEvent(
                payment.Id,
                bookingId,
                amount,
                payment.PaymentDate
            );

            return (payment, paymentEvent, change);
        }

        public BookingCancelledEvent CancelBooking(Guid bookingId, string reason)
        {
            var booking =
                _data.Bookings.FirstOrDefault(b => b.Id == bookingId)
                ?? throw new Exception("Бронирование не найдено.");

            booking.Status = BookingStatus.Cancelled;
            SaveChanges();

            return new BookingCancelledEvent(booking.Id, reason, DateTime.UtcNow);
        }
    }
}
