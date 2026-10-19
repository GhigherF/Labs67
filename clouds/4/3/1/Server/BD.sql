PRAGMA foreign_keys = ON;

BEGIN TRANSACTION;

-- Удаление старых таблиц в правильном порядке
DROP TABLE IF EXISTS Payments;
DROP TABLE IF EXISTS Bookings;
DROP TABLE IF EXISTS Guests;
DROP TABLE IF EXISTS Rooms;
DROP TABLE IF EXISTS Hotels;

-- =========================================
-- Отели
-- =========================================
CREATE TABLE Hotels (
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Address TEXT NOT NULL
);

-- =========================================
-- Номера
-- =========================================
CREATE TABLE Rooms (
    Id TEXT PRIMARY KEY,
    HotelId TEXT NOT NULL,
    RoomNumber TEXT NOT NULL,
    PricePerNight REAL NOT NULL CHECK (PricePerNight >= 0),

    FOREIGN KEY (HotelId)
        REFERENCES Hotels(Id)
        ON DELETE CASCADE,

    UNIQUE (HotelId, RoomNumber)
);

-- =========================================
-- Гости
-- =========================================
CREATE TABLE Guests (
    Id TEXT PRIMARY KEY,
    FullName TEXT NOT NULL,
    Email TEXT NOT NULL DEFAULT ''
);

-- =========================================
-- Бронирования
-- Status: 0 = Created, 1 = Paid, 2 = Cancelled
-- =========================================
CREATE TABLE Bookings (
    Id TEXT PRIMARY KEY,
    GuestId TEXT NOT NULL,
    RoomId TEXT NOT NULL,
    CheckInDate TEXT NOT NULL,
    CheckOutDate TEXT NOT NULL,
    TotalPrice REAL NOT NULL CHECK (TotalPrice >= 0),
    Status INTEGER NOT NULL DEFAULT 0
        CHECK (Status IN (0, 1, 2)),

    FOREIGN KEY (GuestId)
        REFERENCES Guests(Id),

    FOREIGN KEY (RoomId)
        REFERENCES Rooms(Id),

    CHECK (CheckOutDate > CheckInDate)
);

-- =========================================
-- Платежи
-- =========================================
CREATE TABLE Payments (
    Id TEXT PRIMARY KEY,
    BookingId TEXT NOT NULL,
    Amount REAL NOT NULL CHECK (Amount >= 0),
    PaymentDate TEXT NOT NULL,

    FOREIGN KEY (BookingId)
        REFERENCES Bookings(Id)
        ON DELETE CASCADE
);

-- =========================================
-- Индексы
-- =========================================
CREATE INDEX IX_Rooms_HotelId
    ON Rooms(HotelId);

CREATE INDEX IX_Bookings_GuestId
    ON Bookings(GuestId);

CREATE INDEX IX_Bookings_RoomId
    ON Bookings(RoomId);

CREATE INDEX IX_Bookings_Dates
    ON Bookings(CheckInDate, CheckOutDate);

CREATE INDEX IX_Payments_BookingId
    ON Payments(BookingId);

-- =========================================
-- Начальные данные: отель
-- =========================================
INSERT INTO Hotels (Id, Name, Address)
VALUES (
    '11111111-1111-1111-1111-111111111111',
    'Гранд Отель',
    'ул. Ленина 1'
);

-- =========================================
-- Начальные данные: номера
-- =========================================
INSERT INTO Rooms (Id, HotelId, RoomNumber, PricePerNight)
VALUES
(
    'a1b2c3d4-0001-4000-8000-000000000101',
    '11111111-1111-1111-1111-111111111111',
    '101 (Стандарт)',
    1500.0
),
(
    'a1b2c3d4-0002-4000-8000-000000000102',
    '11111111-1111-1111-1111-111111111111',
    '102 (Люкс)',
    3500.0
),
(
    'a1b2c3d4-0003-4000-8000-000000000103',
    '11111111-1111-1111-1111-111111111111',
    '103 (Президентский)',
    7000.0
);

-- =========================================
-- Начальные данные: гости
-- =========================================
INSERT INTO Guests (Id, FullName, Email)
VALUES
(
    '22222222-2222-2222-2222-222222222222',
    'Иван Иванов',
    'ivan@example.com'
),
(
    'ef30fd62-1f8c-4be6-ac85-cd439a89fd66',
    'Freddy Fazbear',
    ''
),
(
    '72ef675f-2b64-41e6-b4a3-8bacb10917c7',
    'Gg',
    ''
),
(
    '4d15c4c7-6c90-474d-9a48-81c03a1b4b57',
    'new',
    ''
);

-- =========================================
-- Начальные данные: бронирования
-- =========================================
INSERT INTO Bookings (
    Id, GuestId, RoomId,
    CheckInDate, CheckOutDate,
    TotalPrice, Status
)
VALUES
(
    'd6b4065f-f240-416b-9f97-595fef700f20',
    '22222222-2222-2222-2222-222222222222',
    'a1b2c3d4-0001-4000-8000-000000000101',
    '2026-12-12T00:00:00',
    '2026-12-16T00:00:00',
    6000.0, 2
),
(
    '75e0e482-bb54-471b-9a6c-ceda3c17870d',
    'ef30fd62-1f8c-4be6-ac85-cd439a89fd66',
    'a1b2c3d4-0001-4000-8000-000000000101',
    '2026-12-24T00:00:00',
    '2026-12-28T00:00:00',
    6000.0, 1
),
(
    'ee4e5a3d-bc42-4816-acd9-fa9d2327ecf7',
    '22222222-2222-2222-2222-222222222222',
    'a1b2c3d4-0003-4000-8000-000000000103',
    '2026-10-20T00:00:00',
    '2027-11-24T00:00:00',
    2800000.0, 1
),
(
    '282475f3-1f2b-481b-b418-d693e28b135e',
    'ef30fd62-1f8c-4be6-ac85-cd439a89fd66',
    'a1b2c3d4-0002-4000-8000-000000000102',
    '2026-08-11T00:00:00',
    '2026-08-13T00:00:00',
    7000.0, 2
),
(
    '733d8865-55a6-4cb8-83af-cd716bd6634b',
    '4d15c4c7-6c90-474d-9a48-81c03a1b4b57',
    'a1b2c3d4-0002-4000-8000-000000000102',
    '2026-09-16T00:00:00',
    '2026-09-18T00:00:00',
    7000.0, 0
),
(
    'b7ea8b82-1df1-43b6-8d81-bb7246aa83cb',
    'ef30fd62-1f8c-4be6-ac85-cd439a89fd66',
    'a1b2c3d4-0001-4000-8000-000000000101',
    '2027-01-01T00:00:00',
    '2027-02-02T00:00:00',
    48000.0, 0
),
(
    '89864ff5-ef8a-4dea-8324-4744d578ef7b',
    'ef30fd62-1f8c-4be6-ac85-cd439a89fd66',
    'a1b2c3d4-0001-4000-8000-000000000101',
    '2028-01-01T00:00:00',
    '2028-02-02T00:00:00',
    48000.0, 0
),
(
    'cc2c7317-91be-44b9-adee-2de19c229fad',
    'ef30fd62-1f8c-4be6-ac85-cd439a89fd66',
    'a1b2c3d4-0001-4000-8000-000000000101',
    '2029-01-01T00:00:00',
    '2029-02-02T00:00:00',
    48000.0, 0
),
(
    'e860fe02-9869-493a-8868-e8bd74025cd9',
    'ef30fd62-1f8c-4be6-ac85-cd439a89fd66',
    'a1b2c3d4-0001-4000-8000-000000000101',
    '2030-01-01T00:00:00',
    '2030-02-02T00:00:00',
    48000.0, 0
),
(
    '9c4039fa-6f1c-405e-9d7a-4f780324dccc',
    'ef30fd62-1f8c-4be6-ac85-cd439a89fd66',
    'a1b2c3d4-0001-4000-8000-000000000101',
    '2030-01-01T00:00:00',
    '2030-02-02T00:00:00',
    48000.0, 0
),
(
    '932ddb4f-50af-4d33-8faf-4fdd248ff53a',
    'ef30fd62-1f8c-4be6-ac85-cd439a89fd66',
    'a1b2c3d4-0001-4000-8000-000000000101',
    '2031-01-01T00:00:00',
    '2031-02-02T00:00:00',
    48000.0, 0
),
(
    '5c9e272d-69dc-4bd7-88f3-f74c0e34b19e',
    'ef30fd62-1f8c-4be6-ac85-cd439a89fd66',
    'a1b2c3d4-0001-4000-8000-000000000101',
    '2031-01-01T00:00:00',
    '2031-02-02T00:00:00',
    48000.0, 0
),
(
    '60b34b57-7fe2-47a2-83ea-ce58260375b5',
    'ef30fd62-1f8c-4be6-ac85-cd439a89fd66',
    'a1b2c3d4-0001-4000-8000-000000000101',
    '2032-01-01T00:00:00',
    '2032-02-02T00:00:00',
    48000.0, 0
),
(
    '683a3bdd-3dab-4a63-82da-e5ee87865860',
    'ef30fd62-1f8c-4be6-ac85-cd439a89fd66',
    'a1b2c3d4-0001-4000-8000-000000000101',
    '2032-01-01T00:00:00',
    '2032-02-02T00:00:00',
    48000.0, 0
),
(
    'f1ef262e-f205-4b58-94f0-a513a0fbb9f5',
    'ef30fd62-1f8c-4be6-ac85-cd439a89fd66',
    'a1b2c3d4-0001-4000-8000-000000000101',
    '2040-01-01T00:00:00',
    '2040-02-02T00:00:00',
    48000.0, 0
),
(
    '2b0b1ae2-2b51-418a-99c4-fd3527c592f8',
    'ef30fd62-1f8c-4be6-ac85-cd439a89fd66',
    'a1b2c3d4-0001-4000-8000-000000000101',
    '2050-01-01T00:00:00',
    '2050-02-02T00:00:00',
    48000.0, 0
),
(
    '49640246-aeb4-4c1b-9834-8259faba0915',
    'ef30fd62-1f8c-4be6-ac85-cd439a89fd66',
    'a1b2c3d4-0001-4000-8000-000000000101',
    '2100-01-01T00:00:00',
    '2100-02-02T00:00:00',
    48000.0, 0
);

-- =========================================
-- Начальные данные: платежи
-- =========================================
INSERT INTO Payments (Id, BookingId, Amount, PaymentDate)
VALUES
(
    '20a518f0-3d74-45de-bb76-67adbda077c6',
    '75e0e482-bb54-471b-9a6c-ceda3c17870d',
    7000,
    '2026-09-08T16:04:08.9456982Z'
),
(
    '9ebcda35-6551-4cf5-be1c-809044cb9815',
    'd6b4065f-f240-416b-9f97-595fef700f20',
    24000,
    '2026-09-08T16:15:20.9755234Z'
),
(
    '888ec144-f281-4950-bd39-e1690c235baf',
    'ee4e5a3d-bc42-4816-acd9-fa9d2327ecf7',
    3000000,
    '2026-09-08T16:23:23.1733776Z'
),
(
    'c91b33e9-2c20-4e9f-b2c5-166f0715a1b1',
    '282475f3-1f2b-481b-b418-d693e28b135e',
    8000,
    '2026-09-08T16:30:02.1902982Z'
);

COMMIT;

-- Проверка количества записей
SELECT 'Hotels' AS TableName, COUNT(*) AS RowCount FROM Hotels
UNION ALL
SELECT 'Rooms', COUNT(*) FROM Rooms
UNION ALL
SELECT 'Guests', COUNT(*) FROM Guests
UNION ALL
SELECT 'Bookings', COUNT(*) FROM Bookings
UNION ALL
SELECT 'Payments', COUNT(*) FROM Payments;
