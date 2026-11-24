using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
        //
        private const bool UseTransactions = true;

        //
        private const bool InjectPaymentError = true;

        ///
        private readonly string _primaryConnectionString;
        private readonly string _replicaConnectionString;
        private volatile bool _primaryAvailable = true;
        private volatile bool _replicaAvailable = true;
        private readonly object _databaseLock = new();

        public enum DatabaseAvailability
        {
            AVAILABLE,
            UNAVAILABLE,
        }

        public record DatabaseStatus(
            DatabaseAvailability Primary,
            DatabaseAvailability Replica,
            string ActiveDatabase
        );

        public record AvailabilityRequest(bool Available);

        private readonly ILogger<BookingService> _logger;
        private readonly object _bookingLock = new();

        public BookingService(
            ILogger<BookingService> logger,
            IConfiguration configuration,
            IWebHostEnvironment environment
        )
        {
            _logger = logger;

            var primaryBuilder = new SqliteConnectionStringBuilder(
                configuration.GetConnectionString("HotelPrimaryDb")
                    ?? "Data Source=hotel_primary.db"
            )
            {
                ForeignKeys = true,
            };

            var replicaBuilder = new SqliteConnectionStringBuilder(
                configuration.GetConnectionString("HotelReplicaDb")
                    ?? "Data Source=hotel_replica.db"
            )
            {
                ForeignKeys = true,
            };

            if (!Path.IsPathRooted(primaryBuilder.DataSource))
            {
                primaryBuilder.DataSource = Path.Combine(
                    environment.ContentRootPath,
                    primaryBuilder.DataSource
                );
            }

            if (!Path.IsPathRooted(replicaBuilder.DataSource))
            {
                replicaBuilder.DataSource = Path.Combine(
                    environment.ContentRootPath,
                    replicaBuilder.DataSource
                );
            }

            _primaryConnectionString = primaryBuilder.ToString();
            _replicaConnectionString = replicaBuilder.ToString();

            InitializeDatabase(_primaryConnectionString, "Primary");
            InitializeDatabase(_replicaConnectionString, "Replica");
        }

        private SqliteConnection OpenConnection()
        {
            string connectionString;
            string databaseName;

            if (_primaryAvailable)
            {
                connectionString = _primaryConnectionString;
                databaseName = "Primary";
            }
            else if (_replicaAvailable)
            {
                connectionString = _replicaConnectionString;
                databaseName = "Replica";
            }
            else
            {
                _logger.LogError("Primary и Replica недоступны!");

                throw new InvalidOperationException("Все экземпляры базы данных недоступны.");
            }

            var connection = new SqliteConnection(connectionString);

            try
            {
                connection.Open();

                _logger.LogInformation("Database access: {Database} | AVAILABLE", databaseName);

                return connection;
            }
            catch
            {
                connection.Dispose();

                _logger.LogError("Database access failed: {Database}", databaseName);

                throw;
            }
        }

        private void ReplicatePrimaryToReplica()
        {
            if (!_primaryAvailable || !_replicaAvailable)
            {
                return;
            }

            lock (_databaseLock)
            {
                if (!_primaryAvailable || !_replicaAvailable)
                {
                    return;
                }

                try
                {
                    using var primary = new SqliteConnection(_primaryConnectionString);

                    using var replica = new SqliteConnection(_replicaConnectionString);

                    primary.Open();
                    replica.Open();

                    primary.BackupDatabase(replica);

                    _logger.LogInformation("Replication completed: Primary -> Replica");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Replication failed: Primary -> Replica");
                }
            }
        }

        public DatabaseStatus GetDatabaseStatus()
        {
            string activeDatabase =
                _primaryAvailable ? "Primary"
                : _replicaAvailable ? "Replica"
                : "NONE";

            return new DatabaseStatus(
                _primaryAvailable
                    ? DatabaseAvailability.AVAILABLE
                    : DatabaseAvailability.UNAVAILABLE,
                _replicaAvailable
                    ? DatabaseAvailability.AVAILABLE
                    : DatabaseAvailability.UNAVAILABLE,
                activeDatabase
            );
        }

        public void SetPrimaryAvailable(bool available)
        {
            lock (_databaseLock)
            {
                if (available && !_primaryAvailable)
                {
                    if (!_replicaAvailable)
                    {
                        throw new InvalidOperationException(
                            "Невозможно восстановить Primary: Replica недоступна."
                        );
                    }

                    using var replica = new SqliteConnection(_replicaConnectionString);

                    using var primary = new SqliteConnection(_primaryConnectionString);

                    replica.Open();
                    primary.Open();

                    replica.BackupDatabase(primary);

                    _logger.LogInformation(
                        "Recovery synchronization completed: Replica -> Primary"
                    );
                }

                _primaryAvailable = available;

                _logger.LogWarning(
                    "Primary availability changed: {Status}",
                    available ? "AVAILABLE" : "UNAVAILABLE"
                );
            }
        }

        public void SetReplicaAvailable(bool available)
        {
            lock (_databaseLock)
            {
                if (available && !_replicaAvailable && _primaryAvailable)
                {
                    using var primary = new SqliteConnection(_primaryConnectionString);

                    using var replica = new SqliteConnection(_replicaConnectionString);

                    primary.Open();
                    replica.Open();

                    primary.BackupDatabase(replica);

                    _logger.LogInformation("Replica synchronized with Primary");
                }

                _replicaAvailable = available;

                _logger.LogWarning(
                    "Replica availability changed: {Status}",
                    available ? "AVAILABLE" : "UNAVAILABLE"
                );
            }
        }

        private void InitializeDatabase(string connectionString, string databaseName)
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();

            command.CommandText = """
                CREATE TABLE IF NOT EXISTS Hotels (
                    Id TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Address TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Rooms (
                    Id TEXT PRIMARY KEY,
                    HotelId TEXT NOT NULL,
                    RoomNumber TEXT NOT NULL,
                    PricePerNight NUMERIC NOT NULL CHECK (PricePerNight >= 0),
                    UNIQUE (HotelId, RoomNumber),
                    FOREIGN KEY (HotelId) REFERENCES Hotels(Id)
                );

                CREATE TABLE IF NOT EXISTS Guests (
                    Id TEXT PRIMARY KEY,
                    FullName TEXT NOT NULL,
                    Email TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Bookings (
                    Id TEXT PRIMARY KEY,
                    GuestId TEXT NOT NULL,
                    RoomId TEXT NOT NULL,
                    CheckInDate TEXT NOT NULL,
                    CheckOutDate TEXT NOT NULL,
                    TotalPrice NUMERIC NOT NULL CHECK (TotalPrice >= 0),
                    Status INTEGER NOT NULL DEFAULT 0
                        CHECK (Status IN (0, 1, 2)),
                    CHECK (CheckOutDate > CheckInDate),
                    FOREIGN KEY (GuestId) REFERENCES Guests(Id),
                    FOREIGN KEY (RoomId) REFERENCES Rooms(Id)
                );

                CREATE TABLE IF NOT EXISTS Payments (
                    Id TEXT PRIMARY KEY,
                    BookingId TEXT NOT NULL,
                    Amount NUMERIC NOT NULL CHECK (Amount >= 0),
                    PaymentDate TEXT NOT NULL,
                    FOREIGN KEY (BookingId) REFERENCES Bookings(Id)
                );

                CREATE INDEX IF NOT EXISTS IX_Bookings_RoomId
                    ON Bookings(RoomId);

                CREATE INDEX IF NOT EXISTS IX_Bookings_GuestId
                    ON Bookings(GuestId);

                CREATE INDEX IF NOT EXISTS IX_Payments_BookingId
                    ON Payments(BookingId);
                """;

            command.ExecuteNonQuery();
            _logger.LogInformation(
                "SQLite {DatabaseName} initialized: {Database}",
                databaseName,
                new SqliteConnectionStringBuilder(connectionString).DataSource
            );
        }

        private static string FormatDate(DateTime value) =>
            value.ToString("O", CultureInfo.InvariantCulture);

        private static DateTime ReadDate(SqliteDataReader reader, int index) =>
            DateTime.Parse(
                reader.GetString(index),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind
            );

        private static decimal ReadDecimal(SqliteDataReader reader, int index) =>
            Convert.ToDecimal(reader.GetValue(index), CultureInfo.InvariantCulture);

        private static void AddParameter(SqliteCommand command, string name, object value) =>
            command.Parameters.AddWithValue(name, value);

        private static void SetTransaction(SqliteCommand command, SqliteTransaction? transaction)
        {
            if (transaction is not null)
                command.Transaction = transaction;
        }

        public AppData Data => GetData();

        public AppData GetData()
        {
            var data = new AppData();

            using var connection = OpenConnection();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Id, Name, Address FROM Hotels";

                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    data.Hotels.Add(
                        new Hotel
                        {
                            Id = Guid.Parse(reader.GetString(0)),
                            Name = reader.GetString(1),
                            Address = reader.GetString(2),
                        }
                    );
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT Id, HotelId, RoomNumber, PricePerNight FROM Rooms
                    """;

                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    data.Rooms.Add(
                        new Room
                        {
                            Id = Guid.Parse(reader.GetString(0)),
                            HotelId = Guid.Parse(reader.GetString(1)),
                            RoomNumber = reader.GetString(2),
                            PricePerNight = ReadDecimal(reader, 3),
                        }
                    );
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Id, FullName, Email FROM Guests";

                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    data.Guests.Add(
                        new Guest
                        {
                            Id = Guid.Parse(reader.GetString(0)),
                            FullName = reader.GetString(1),
                            Email = reader.GetString(2),
                        }
                    );
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT Id, GuestId, RoomId, CheckInDate, CheckOutDate,
                           TotalPrice, Status
                    FROM Bookings
                    """;

                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    data.Bookings.Add(
                        new Booking
                        {
                            Id = Guid.Parse(reader.GetString(0)),
                            GuestId = Guid.Parse(reader.GetString(1)),
                            RoomId = Guid.Parse(reader.GetString(2)),
                            CheckInDate = ReadDate(reader, 3),
                            CheckOutDate = ReadDate(reader, 4),
                            TotalPrice = ReadDecimal(reader, 5),
                            Status = (BookingStatus)reader.GetInt32(6),
                        }
                    );
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT Id, BookingId, Amount, PaymentDate FROM Payments
                    """;

                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    data.Payments.Add(
                        new Payment
                        {
                            Id = Guid.Parse(reader.GetString(0)),
                            BookingId = Guid.Parse(reader.GetString(1)),
                            Amount = ReadDecimal(reader, 2),
                            PaymentDate = ReadDate(reader, 3),
                        }
                    );
                }
            }

            return data;
        }

        public Guest AddGuest(string fullName, string email = "")
        {
            var guest = new Guest { FullName = fullName, Email = email };

            using var connection = OpenConnection();
            using var command = connection.CreateCommand();

            command.CommandText = """
                INSERT INTO Guests (Id, FullName, Email)
                VALUES ($id, $name, $email)
                """;

            AddParameter(command, "$id", guest.Id.ToString());
            AddParameter(command, "$name", guest.FullName);
            AddParameter(command, "$email", guest.Email);

            command.ExecuteNonQuery();
            ReplicatePrimaryToReplica();
            return guest;
        }

        public bool CheckAvailability(Guid roomId, DateTime checkIn, DateTime checkOut)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();

            command.CommandText = """
                SELECT NOT EXISTS (
                    SELECT 1
                    FROM Bookings
                    WHERE RoomId = $roomId
                      AND Status <> $cancelled
                      AND $checkIn < CheckOutDate
                      AND $checkOut > CheckInDate
                )
                """;

            AddParameter(command, "$roomId", roomId.ToString());
            AddParameter(command, "$cancelled", (int)BookingStatus.Cancelled);
            AddParameter(command, "$checkIn", FormatDate(checkIn));
            AddParameter(command, "$checkOut", FormatDate(checkOut));

            return Convert.ToInt32(command.ExecuteScalar()) == 1;
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
            decimal pricePerNight,
            string clientName
        )
        {
            _logger.LogInformation("{Client} → START Reserve", clientName);

            lock (_bookingLock)
            {
                using var connection = OpenConnection();

                SqliteTransaction? transaction = UseTransactions
                    ? connection.BeginTransaction()
                    : null;

                try
                {
                    if (checkOut <= checkIn)
                        throw new ArgumentException("Дата выезда должна быть позже даты заезда.");

                    using (var command = connection.CreateCommand())
                    {
                        SetTransaction(command, transaction);

                        command.CommandText = """
                            SELECT NOT EXISTS (
                                SELECT 1
                                FROM Bookings
                                WHERE RoomId = $roomId
                                  AND Status <> $cancelled
                                  AND $checkIn < CheckOutDate
                                  AND $checkOut > CheckInDate
                            )
                            """;

                        AddParameter(command, "$roomId", roomId.ToString());
                        AddParameter(command, "$cancelled", (int)BookingStatus.Cancelled);
                        AddParameter(command, "$checkIn", FormatDate(checkIn));
                        AddParameter(command, "$checkOut", FormatDate(checkOut));

                        if (Convert.ToInt32(command.ExecuteScalar()) != 1)
                            throw new InvalidOperationException("Номер занят на указанные даты.");
                    }

                    var days = (checkOut.Date - checkIn.Date).Days;

                    if (days <= 0)
                        throw new ArgumentException(
                            "Бронирование должно длиться хотя бы одну ночь."
                        );

                    if (pricePerNight < 0)
                        throw new ArgumentOutOfRangeException(nameof(pricePerNight));

                    var booking = new Booking
                    {
                        GuestId = guestId,
                        RoomId = roomId,
                        CheckInDate = checkIn,
                        CheckOutDate = checkOut,
                        TotalPrice = days * pricePerNight,
                        Status = BookingStatus.Created,
                    };

                    using (var command = connection.CreateCommand())
                    {
                        SetTransaction(command, transaction);

                        command.CommandText = """
                            INSERT INTO Bookings (
                                Id, GuestId, RoomId, CheckInDate,
                                CheckOutDate, TotalPrice, Status
                            )
                            VALUES (
                                $id, $guest, $room, $checkIn,
                                $checkOut, $price, $status
                            )
                            """;

                        AddParameter(command, "$id", booking.Id.ToString());
                        AddParameter(command, "$guest", guestId.ToString());
                        AddParameter(command, "$room", roomId.ToString());
                        AddParameter(command, "$checkIn", FormatDate(checkIn));
                        AddParameter(command, "$checkOut", FormatDate(checkOut));
                        AddParameter(command, "$price", booking.TotalPrice);
                        AddParameter(command, "$status", (int)booking.Status);

                        command.ExecuteNonQuery();
                    }

                    // if (UseTransactions)
                    //     transaction!.Commit();

                    if (UseTransactions)
                        transaction!.Commit();
                    ReplicatePrimaryToReplica();
                    var createdEvent = new BookingCreatedEvent(
                        booking.Id,
                        guestId,
                        roomId,
                        checkIn,
                        checkOut,
                        booking.TotalPrice
                    );

                    var reservedEvent = new RoomReservedEvent(
                        roomId,
                        booking.Id,
                        checkIn,
                        checkOut
                    );

                    _logger.LogInformation("{Client} → SUCCESS", clientName);

                    return (booking, createdEvent, reservedEvent);
                }
                catch
                {
                    if (UseTransactions && transaction is not null)
                    {
                        transaction.Rollback();
                    }

                    throw;
                }
                finally
                {
                    transaction?.Dispose();

                    _logger.LogInformation("{Client} → UNLOCK", clientName);
                }
            }
        }

        public (Payment payment, PaymentCompletedEvent paymentEvent, decimal change) PayBooking(
            Guid bookingId,
            decimal amount
        )
        {
            using var connection = OpenConnection();

            SqliteTransaction? transaction = UseTransactions ? connection.BeginTransaction() : null;

            try
            {
                Booking booking;

                using (var command = connection.CreateCommand())
                {
                    SetTransaction(command, transaction);

                    command.CommandText = """
                        SELECT Id, GuestId, RoomId, CheckInDate, CheckOutDate,
                               TotalPrice, Status
                        FROM Bookings
                        WHERE Id = $id
                        """;

                    AddParameter(command, "$id", bookingId.ToString());

                    using var reader = command.ExecuteReader();

                    if (!reader.Read())
                        throw new InvalidOperationException("Бронирование не найдено.");

                    booking = new Booking
                    {
                        Id = Guid.Parse(reader.GetString(0)),
                        GuestId = Guid.Parse(reader.GetString(1)),
                        RoomId = Guid.Parse(reader.GetString(2)),
                        CheckInDate = ReadDate(reader, 3),
                        CheckOutDate = ReadDate(reader, 4),
                        TotalPrice = ReadDecimal(reader, 5),
                        Status = (BookingStatus)reader.GetInt32(6),
                    };
                }

                if (booking.Status == BookingStatus.Paid)
                    throw new InvalidOperationException("Данное бронирование уже оплачено.");

                if (booking.Status == BookingStatus.Cancelled)
                    throw new InvalidOperationException("Нельзя оплатить отмененное бронирование.");

                if (amount < booking.TotalPrice)
                    throw new InvalidOperationException(
                        $"Недостаточно средств. К оплате: {booking.TotalPrice:C2}"
                    );

                if (amount < 0)
                    throw new ArgumentOutOfRangeException(nameof(amount));

                using (var command = connection.CreateCommand())
                {
                    SetTransaction(command, transaction);

                    command.CommandText = """
                        UPDATE Bookings
                        SET Status = $paid
                        WHERE Id = $id AND Status = $created
                        """;

                    AddParameter(command, "$paid", (int)BookingStatus.Paid);
                    AddParameter(command, "$created", (int)BookingStatus.Created);
                    AddParameter(command, "$id", bookingId.ToString());

                    if (command.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException(
                            "Не удалось обновить статус бронирования."
                        );
                }

                _logger.LogInformation("Booking status updated to Paid: {BookingId}", bookingId);

                if (InjectPaymentError)
                    throw new InvalidOperationException(
                        "Искусственная ошибка между UPDATE и INSERT."
                    );

                var payment = new Payment
                {
                    BookingId = bookingId,
                    Amount = amount,
                    PaymentDate = DateTime.UtcNow,
                };

                using (var command = connection.CreateCommand())
                {
                    SetTransaction(command, transaction);

                    command.CommandText = """
                        INSERT INTO Payments (Id, BookingId, Amount, PaymentDate)
                        VALUES ($id, $booking, $amount, $date)
                        """;

                    AddParameter(command, "$id", payment.Id.ToString());
                    AddParameter(command, "$booking", bookingId.ToString());
                    AddParameter(command, "$amount", amount);
                    AddParameter(command, "$date", FormatDate(payment.PaymentDate));

                    command.ExecuteNonQuery();
                }

                if (UseTransactions)
                {
                    transaction!.Commit();
                }

                ReplicatePrimaryToReplica();
                var paymentEvent = new PaymentCompletedEvent(
                    payment.Id,
                    bookingId,
                    amount,
                    payment.PaymentDate
                );

                return (payment, paymentEvent, amount - booking.TotalPrice);
            }
            catch
            {
                if (UseTransactions && transaction is not null)
                {
                    transaction.Rollback();
                }

                throw;
            }
            finally
            {
                transaction?.Dispose();
            }
        }

        public BookingCancelledEvent CancelBooking(Guid bookingId, string reason)
        {
            using var connection = OpenConnection();

            SqliteTransaction? transaction = UseTransactions ? connection.BeginTransaction() : null;

            try
            {
                using var command = connection.CreateCommand();
                SetTransaction(command, transaction);

                command.CommandText = """
                    UPDATE Bookings
                    SET Status = $cancelled
                    WHERE Id = $id AND Status <> $cancelled
                    """;

                AddParameter(command, "$cancelled", (int)BookingStatus.Cancelled);
                AddParameter(command, "$id", bookingId.ToString());

                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException(
                        "Бронирование не найдено или уже отменено."
                    );

                if (UseTransactions)
                {
                    // transaction!.Commit();
                    transaction!.Commit();
                }

                ReplicatePrimaryToReplica();
                return new BookingCancelledEvent(bookingId, reason, DateTime.UtcNow);
            }
            catch
            {
                if (UseTransactions && transaction is not null)
                {
                    transaction.Rollback();
                }

                throw;
            }
            finally
            {
                transaction?.Dispose();
            }
        }
    }
}
