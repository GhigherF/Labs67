using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;

[assembly: InternalsVisibleTo("Client.Tests")]

namespace Client;

public class Program
{
    public static readonly HttpClient httpClient = new HttpClient
    {
        BaseAddress = new Uri("http://localhost:5230/"),
        Timeout = TimeSpan.FromSeconds(25),
    };

    public static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
    };

    public static TimeSpan ClientClockOffset { get; private set; } = TimeSpan.FromSeconds(7);

    public static DateTime ClientNowUtc => DateTime.UtcNow + ClientClockOffset;

    public static void Main(string[] args)
    {
        while (true)
        {
            Console.WriteLine("=== СИСТЕМА УПРАВЛЕНИЯ ОТЕЛЕМ ===");
            Console.WriteLine(
                $"Локальное время клиента: {ClientNowUtc:yyyy-MM-dd HH:mm:ss.fff} UTC"
            );
            Console.WriteLine(
                $"Искусственное смещение: {ClientClockOffset.TotalSeconds:+0.###;-0.###;0} секунд"
            );
            Console.WriteLine("--------------------------------");
            Console.WriteLine("1. Посмотреть список номеров");
            Console.WriteLine("2. Посмотреть список гостей");
            Console.WriteLine("3. Добавить нового гостя");
            Console.WriteLine("4. Проверить доступность номера");
            Console.WriteLine("5. Создать бронирование");
            Console.WriteLine("6. Посмотреть список бронирований");
            Console.WriteLine("7. Оплатить бронирование");
            Console.WriteLine("8. Отменить бронирование");
            Console.WriteLine("9. Показать локальное время клиента");
            Console.WriteLine("10. Настроить смещение часов клиента");
            Console.WriteLine("11. Синхронизировать время с сервером");
            Console.WriteLine("12. Эксперимент: оплата бронирования");
            Console.WriteLine("0. Выход");
            Console.WriteLine("================================");
            Console.Write("Выберите действие: ");

            var choice = Console.ReadLine()?.Trim();

            try
            {
                switch (choice)
                {
                    case "1":
                        ShowRooms();
                        break;
                    case "2":
                        ShowGuests();
                        break;
                    case "3":
                        CreateGuest();
                        break;
                    case "4":
                        CheckAvailability();
                        break;
                    case "5":
                        CreateBooking();
                        break;
                    case "6":
                        ShowBookings();
                        break;
                    case "7":
                        PayBooking();
                        break;
                    case "8":
                        CancelBooking();
                        break;
                    case "9":
                        ShowClientTime();
                        break;
                    case "10":
                        ConfigureClockOffset();
                        break;
                    case "11":
                        SynchronizeTime();
                        break;
                    case "12":
                        RunPaymentExperiment();
                        break;
                    case "0":
                        return;
                    default:
                        Console.WriteLine("Некорректная операция: указан неизвестный пункт меню.");
                        break;
                }
            }
            catch (Exception ex)
            {
                HandleException(ex, "основном цикле программы");
            }

            Console.WriteLine("\nНажмите Enter, чтобы продолжить...");
            Console.ReadLine();
        }
    }

    public static void ShowClientTime()
    {
        Console.WriteLine("\n ЛОКАЛЬНОЕ ВРЕМЯ КЛИЕНТА");
        Console.WriteLine("----------------------------------------");
        Console.WriteLine($"System UTC time: {DateTime.UtcNow:O}");
        Console.WriteLine(
            $"Client offset:   {ClientClockOffset.TotalSeconds:+0.###;-0.###;0} seconds"
        );
        Console.WriteLine($"Client time:     {ClientNowUtc:O}");
        Console.WriteLine("----------------------------------------");
    }

    public static void ConfigureClockOffset()
    {
        Console.WriteLine("\n НАСТРОЙКА ЧАСОВ КЛИЕНТА");
        Console.WriteLine(
            $"Текущее смещение: {ClientClockOffset.TotalSeconds:+0.###;-0.###;0} секунд"
        );
        Console.Write("Введите новое смещение в секундах (например, 7 или -3): ");

        var input = Console.ReadLine()?.Trim();

        if (
            !double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out var seconds)
            && !double.TryParse(
                input,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out seconds
            )
        )
        {
            Console.WriteLine("Некорректное значение смещения.");
            return;
        }

        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || Math.Abs(seconds) > 86400)
        {
            Console.WriteLine("Смещение должно находиться в диапазоне от -86400 до 86400 секунд.");
            return;
        }

        ClientClockOffset = TimeSpan.FromSeconds(seconds);

        Console.WriteLine("Смещение часов клиента изменено.");
        Console.WriteLine(
            $"Client offset: {ClientClockOffset.TotalSeconds:+0.###;-0.###;0} seconds"
        );
        Console.WriteLine($"Local time: {ClientNowUtc:O}");
    }

    public static void SynchronizeTime()
    {
        Console.WriteLine("\n СИНХРОНИЗАЦИЯ ВРЕМЕНИ");
        Console.WriteLine(
            $"Client offset before synchronization: {ClientClockOffset.TotalSeconds:+0.###;-0.###;0} seconds"
        );

        var localRequestTime = ClientNowUtc;

        Console.WriteLine($"Local time before request: {localRequestTime:O}");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = httpClient.GetAsync("api/time").Result;

            stopwatch.Stop();

            var responseTime = ClientNowUtc;
            var rtt = stopwatch.Elapsed;

            Console.WriteLine($"Request time:  {localRequestTime:O}");
            Console.WriteLine($"Response time: {responseTime:O}");
            Console.WriteLine($"RTT: {rtt.TotalMilliseconds:F2} ms");
            Console.WriteLine(
                $"Estimated network delay (RTT / 2): {rtt.TotalMilliseconds / 2:F2} ms"
            );

            if (!response.IsSuccessStatusCode)
            {
                HandleHttpResponseError(response);
                Console.WriteLine("Синхронизация не выполнена: сервер вернул ошибку.");
                return;
            }

            using var document = JsonDocument.Parse(response.Content.ReadAsStringAsync().Result);

            if (
                !document.RootElement.TryGetProperty("serverTime", out var serverTimeElement)
                || serverTimeElement.ValueKind != JsonValueKind.String
                || !serverTimeElement.TryGetDateTime(out var serverTime)
            )
            {
                Console.WriteLine("Ответ сервера не содержит корректного поля serverTime.");
                Console.WriteLine("Ожидаемый формат: { \"serverTime\": \"2026-10-13T14:30:00Z\" }");
                return;
            }

            serverTime = NormalizeUtc(serverTime);

            var estimatedMidpoint = localRequestTime + TimeSpan.FromTicks(rtt.Ticks / 2);
            var calculatedOffset = serverTime - estimatedMidpoint;
            var correctedTime = responseTime + calculatedOffset;

            Console.WriteLine();
            Console.WriteLine("РЕЗУЛЬТАТЫ СИНХРОНИЗАЦИИ");
            Console.WriteLine("----------------------------------------");
            Console.WriteLine($"Local time:          {responseTime:O}");
            Console.WriteLine($"Server time:         {serverTime:O}");
            Console.WriteLine($"Estimated midpoint:  {estimatedMidpoint:O}");
            Console.WriteLine(
                $"Calculated offset:   {calculatedOffset.TotalMilliseconds:+0.###;-0.###;0} ms"
            );
            Console.WriteLine($"Corrected time:      {correctedTime:O}");
            Console.WriteLine("----------------------------------------");

            ClientClockOffset += calculatedOffset;

            Console.WriteLine(
                $"Updated client offset: {ClientClockOffset.TotalMilliseconds:+0.###;-0.###;0} ms"
            );
            Console.WriteLine($"Client time after correction: {ClientNowUtc:O}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            HandleException(ex, "синхронизации времени");
        }
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }

    public static void HandleException(Exception ex, string context = "")
    {
        var targetEx = ex is AggregateException agg ? agg.InnerException ?? ex : ex;

        switch (targetEx)
        {
            case HttpRequestException httpEx:
                if (
                    httpEx.InnerException is System.Net.Sockets.SocketException
                    || httpEx.StatusCode == null
                )
                {
                    Console.WriteLine(
                        $"Ошибка: Сервер недоступен или отсутствует сетевое подключение ({context})."
                    );
                }
                else
                {
                    Console.WriteLine(
                        $"Ошибка сетевого взаимодействия HTTP (Статус: {httpEx.StatusCode})."
                    );
                }
                break;

            case TaskCanceledException:
                Console.WriteLine("Ошибка: Превышено время ожидания ответа от сервера (Timeout).");
                break;

            case FormatException:
                Console.WriteLine(
                    "Некорректные входные данные: неверный формат числа, даты или GUID."
                );
                break;

            default:
                Console.WriteLine(
                    $"Произошла непредвиденная ошибка ({context}): {targetEx.Message}"
                );
                break;
        }
    }

    public static void HandleHttpResponseError(HttpResponseMessage response)
    {
        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                Console.WriteLine(
                    "Ошибка (404): Запрашиваемый объект или эндпоинт не найден на сервере."
                );
                break;

            case HttpStatusCode.BadRequest:
                var error = TryReadErrorResponse(response);
                Console.WriteLine(
                    $"400 Bad Request: {error ?? "Переданы некорректные входные данные или выполнена недопустимая операция."}"
                );
                break;

            case HttpStatusCode.InternalServerError:
                Console.WriteLine("Ошибка 500: Внутренняя ошибка сервера.");
                break;

            default:
                Console.WriteLine(
                    $"Ошибка ответа сервера: {(int)response.StatusCode} {response.ReasonPhrase}"
                );
                break;
        }
    }

    internal static string? TryReadErrorResponse(HttpResponseMessage response)
    {
        try
        {
            var err = response.Content.ReadFromJsonAsync<ErrorResponse>(jsonOptions).Result;
            return err?.Message;
        }
        catch
        {
            return null;
        }
    }

    internal static List<Room>? GetRooms()
    {
        try
        {
            var response = httpClient.GetAsync("api/rooms").Result;

            if (response.IsSuccessStatusCode)
            {
                return response.Content.ReadFromJsonAsync<List<Room>>(jsonOptions).Result;
            }

            HandleHttpResponseError(response);
            return null;
        }
        catch (Exception ex)
        {
            HandleException(ex, "получении списка комнат");
            return null;
        }
    }

    internal static List<Guest>? GetGuests()
    {
        try
        {
            var response = httpClient.GetAsync("api/guests").Result;

            if (response.IsSuccessStatusCode)
            {
                return response.Content.ReadFromJsonAsync<List<Guest>>(jsonOptions).Result;
            }

            HandleHttpResponseError(response);
            return null;
        }
        catch (Exception ex)
        {
            HandleException(ex, "получении списка гостей");
            return null;
        }
    }

    internal static Room? FindRoomByNumber(IEnumerable<Room> rooms, string inputNumber)
    {
        var cleanInput = inputNumber.Trim();

        if (string.IsNullOrWhiteSpace(cleanInput))
            return null;

        return rooms.FirstOrDefault(r =>
            r.RoomNumber.Equals(cleanInput, StringComparison.OrdinalIgnoreCase)
            || r.RoomNumber.StartsWith(cleanInput + " ", StringComparison.OrdinalIgnoreCase)
            || r.RoomNumber.StartsWith(cleanInput + "(", StringComparison.OrdinalIgnoreCase)
            || r.RoomNumber.Split(' ')[0].Equals(cleanInput, StringComparison.OrdinalIgnoreCase)
        );
    }

    internal static Guest? FindGuestByNameOrIndex(IEnumerable<Guest> guests, string input)
    {
        var clean = input.Trim();

        if (string.IsNullOrWhiteSpace(clean))
            return null;

        return guests.FirstOrDefault(g =>
            g.FullName.Equals(clean, StringComparison.OrdinalIgnoreCase)
            || g.FullName.Contains(clean, StringComparison.OrdinalIgnoreCase)
        );
    }

    internal static void ShowRooms()
    {
        Console.WriteLine("\n СПИСОК НОМЕРОВ:");
        Console.WriteLine("----------------------------------------------------------------------");
        Console.WriteLine($"{"№ Номера", -20} | {"Цена / Ночь", -15} | {"GUID номера", -36}");
        Console.WriteLine("----------------------------------------------------------------------");

        var rooms = GetRooms();

        if (rooms == null || rooms.Count == 0)
        {
            Console.WriteLine("Записи не найдены или произошла ошибка подключения.");
            return;
        }

        foreach (var room in rooms)
        {
            Console.WriteLine($"{room.RoomNumber, -20} | {room.PricePerNight, 12:C2} | {room.Id}");
        }
    }

    internal static void ShowGuests()
    {
        Console.WriteLine("\n СПИСОК ГОСТЕЙ:");
        Console.WriteLine("----------------------------------------------------------------------");
        Console.WriteLine($"{"ФИО Гостя", -30} | {"GUID Гостя", -36}");
        Console.WriteLine("----------------------------------------------------------------------");

        var guests = GetGuests();

        if (guests == null || guests.Count == 0)
        {
            Console.WriteLine("Записи не найдены или произошла ошибка подключения.");
            return;
        }

        foreach (var guest in guests)
        {
            Console.WriteLine($"{guest.FullName, -30} | {guest.Id}");
        }
    }

    internal static void CreateGuest()
    {
        Console.WriteLine("\n ДОБАВЛЕНИЕ НОВОГО ГОСТЯ:");
        Console.Write("Введите ФИО гостя: ");

        var fullName = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(fullName))
        {
            Console.WriteLine("Некорректные входные данные: ФИО не может быть пустым.");
            return;
        }

        try
        {
            var dto = new CreateGuestRequest(fullName);
            var response = httpClient.PostAsJsonAsync("api/guests", dto).Result;

            if (response.IsSuccessStatusCode)
            {
                var createdGuest = response.Content.ReadFromJsonAsync<Guest>(jsonOptions).Result;

                Console.WriteLine("Гость успешно добавлен!");

                if (createdGuest != null)
                {
                    Console.WriteLine($"ID: {createdGuest.Id} | ФИО: {createdGuest.FullName}");
                }
            }
            else
            {
                HandleHttpResponseError(response);
            }
        }
        catch (Exception ex)
        {
            HandleException(ex, "создании гостя");
        }
    }

    internal static void CheckAvailability()
    {
        var rooms = GetRooms();

        if (rooms == null || rooms.Count == 0)
            return;

        Console.Write("Введите номер комнаты (например, 101): ");
        var roomInput = Console.ReadLine() ?? "";
        var room = FindRoomByNumber(rooms, roomInput);

        if (room == null)
        {
            Console.WriteLine($"Отсутствующий объект: Номер '{roomInput}' не найден.");
            return;
        }

        Console.Write("Дата заезда (ГГГГ-ММ-ДД): ");

        if (!DateTime.TryParse(Console.ReadLine(), out var checkIn))
        {
            Console.WriteLine("Некорректные входные данные: некорректный формат даты заезда.");
            return;
        }

        Console.Write("Дата выезда (ГГГГ-ММ-ДД): ");

        if (!DateTime.TryParse(Console.ReadLine(), out var checkOut))
        {
            Console.WriteLine("Некорректные входные данные: некорректный формат даты выезда.");
            return;
        }

        try
        {
            var url =
                $"api/check-availability?roomId={room.Id}&checkIn={checkIn:yyyy-MM-dd}&checkOut={checkOut:yyyy-MM-dd}";

            var response = httpClient.GetAsync(url).Result;

            if (response.IsSuccessStatusCode)
            {
                var result = response
                    .Content.ReadFromJsonAsync<AvailabilityResponse>(jsonOptions)
                    .Result;

                if (result != null && result.IsAvailable)
                {
                    Console.WriteLine($"Номер {room.RoomNumber} СВОБОДЕН на указанные даты.");
                }
                else
                {
                    Console.WriteLine($"Номер {room.RoomNumber} ЗАНЯТ на указанные даты.");
                }
            }
            else
            {
                HandleHttpResponseError(response);
            }
        }
        catch (Exception ex)
        {
            HandleException(ex, "проверке доступности номера");
        }
    }

    internal static void CreateBooking()
    {
        var rooms = GetRooms();
        var guests = GetGuests();

        if (rooms == null || rooms.Count == 0 || guests == null || guests.Count == 0)
        {
            return;
        }

        Console.Write("Введите номер комнаты (например, 101): ");
        var roomInput = Console.ReadLine() ?? "";
        var room = FindRoomByNumber(rooms, roomInput);

        if (room == null)
        {
            Console.WriteLine($"Отсутствующий объект: Номер '{roomInput}' не найден.");
            return;
        }

        Console.Write("Введите ФИО или часть имени гостя: ");
        var guestInput = Console.ReadLine() ?? "";
        var guest = FindGuestByNameOrIndex(guests, guestInput);

        if (guest == null)
        {
            Console.WriteLine($"Отсутствующий объект: Гость '{guestInput}' не найден.");
            return;
        }

        Console.Write("Дата заезда (ГГГГ-ММ-ДД): ");

        if (!DateTime.TryParse(Console.ReadLine(), out var checkIn))
        {
            Console.WriteLine("Некорректные входные данные: формат даты.");
            return;
        }

        Console.Write("Дата выезда (ГГГГ-ММ-ДД): ");

        if (!DateTime.TryParse(Console.ReadLine(), out var checkOut))
        {
            Console.WriteLine("Некорректные входные данные: формат даты.");
            return;
        }

        try
        {
            var dto = new CreateBookingRequest(guest.Id, room.Id, checkIn, checkOut);
            var response = httpClient.PostAsJsonAsync("api/bookings", dto).Result;

            if (response.IsSuccessStatusCode)
            {
                var result = response
                    .Content.ReadFromJsonAsync<CreateBookingResponse>(jsonOptions)
                    .Result;

                Console.WriteLine("Бронирование успешно создано!");

                if (result?.Booking != null)
                {
                    Console.WriteLine($"ID бронирования: {result.Booking.Id}");
                    Console.WriteLine($"Итоговая сумма: {result.Booking.TotalPrice:C2}");
                }
            }
            else
            {
                HandleHttpResponseError(response);
            }
        }
        catch (Exception ex)
        {
            HandleException(ex, "создании бронирования");
        }
    }

    internal static void ShowBookings()
    {
        Console.WriteLine("\n СПИСОК БРОНИРОВАНИЙ:");
        Console.WriteLine(
            "---------------------------------------------------------------------------------------------------"
        );
        Console.WriteLine(
            $"{"GUID Бронирования", -36} | {"Комната", -12} | {"Заезд", -10} | {"Выезд", -10} | {"Сумма", -10} | {"Статус", -10}"
        );
        Console.WriteLine(
            "---------------------------------------------------------------------------------------------------"
        );

        try
        {
            var response = httpClient.GetAsync("api/bookings").Result;

            if (!response.IsSuccessStatusCode)
            {
                HandleHttpResponseError(response);
                return;
            }

            var bookings = response.Content.ReadFromJsonAsync<List<Booking>>(jsonOptions).Result;
            var rooms = GetRooms() ?? new List<Room>();

            if (bookings == null || bookings.Count == 0)
            {
                Console.WriteLine("Бронирования отсутствуют.");
                return;
            }

            foreach (var b in bookings)
            {
                var room = rooms.FirstOrDefault(r => r.Id == b.RoomId);
                var roomNumberDisplay = room != null ? room.RoomNumber : "Н/Д";

                Console.WriteLine(
                    $"{b.Id, -36} | {roomNumberDisplay, -12} | {b.CheckInDate:yyyy-MM-dd} | {b.CheckOutDate:yyyy-MM-dd} | {b.TotalPrice, 10:C2} | {FormatStatus(b.Status)}"
                );
            }
        }
        catch (Exception ex)
        {
            HandleException(ex, "загрузке бронирований");
        }
    }

    internal static void PayBooking()
    {
        Console.WriteLine("\n ОПЛАТА БРОНИРОВАНИЯ:");
        Console.Write("Введите GUID бронирования для оплаты: ");

        if (!Guid.TryParse(Console.ReadLine(), out var bookingId))
        {
            Console.WriteLine("Некорректные входные данные: неверный формат GUID.");
            return;
        }

        Console.Write("Введите сумму оплаты: ");

        if (!decimal.TryParse(Console.ReadLine(), out var amount))
        {
            Console.WriteLine("Некорректные входные данные: некорректное числовое значение суммы.");
            return;
        }

        try
        {
            var dto = new PayBookingRequest(amount);
            var response = httpClient.PostAsJsonAsync($"api/bookings/{bookingId}/pay", dto).Result;

            if (response.IsSuccessStatusCode)
            {
                var result = response
                    .Content.ReadFromJsonAsync<PayBookingResponse>(jsonOptions)
                    .Result;

                Console.WriteLine("Оплата прошла успешно!");

                if (result?.Change is decimal change && change > 0)
                {
                    Console.WriteLine($"Ваша сдача: {change:C2}");
                }
            }
            else
            {
                HandleHttpResponseError(response);
            }
        }
        catch (Exception ex)
        {
            HandleException(ex, "оплате бронирования");
        }
    }

    internal static void RunPaymentExperiment()
    {
        Console.WriteLine("\n ЭКСПЕРИМЕНТ: ОПЛАТА БРОНИРОВАНИЯ");
        Console.Write("Введите GUID бронирования для эксперимента: ");

        if (!Guid.TryParse(Console.ReadLine(), out var bookingId))
        {
            Console.WriteLine("Некорректные входные данные: неверный формат GUID.");
            return;
        }

        try
        {
            Console.WriteLine("\n1. Получение списка бронирований...");
            using var bookingsResponse = httpClient.GetAsync("api/bookings").Result;

            if (!bookingsResponse.IsSuccessStatusCode)
            {
                HandleHttpResponseError(bookingsResponse);
                return;
            }

            var bookings = bookingsResponse
                .Content.ReadFromJsonAsync<List<Booking>>(jsonOptions)
                .Result;

            var booking = bookings?.FirstOrDefault(b => b.Id == bookingId);

            if (booking == null)
            {
                Console.WriteLine("Бронирование с указанным GUID не найдено.");
                return;
            }

            Console.WriteLine($"Текущий статус: {FormatStatus(booking.Status)}");
            Console.WriteLine($"Сумма бронирования: {booking.TotalPrice:C2}");

            if (booking.Status != BookingStatus.Created)
            {
                Console.WriteLine(
                    "Эксперимент остановлен: бронирование должно иметь статус Created."
                );
                Console.WriteLine(
                    "Для повторного запуска используй GUID другого бронирования со статусом Created."
                );
                return;
            }

            Console.WriteLine("\n2. Отправка запроса на оплату...");
            var paymentRequest = new PayBookingRequest(booking.TotalPrice);

            using var paymentResponse = httpClient
                .PostAsJsonAsync($"api/bookings/{bookingId}/pay", paymentRequest)
                .Result;

            Console.WriteLine(
                $"Ответ оплаты: {(int)paymentResponse.StatusCode} {paymentResponse.ReasonPhrase}"
            );

            if (paymentResponse.IsSuccessStatusCode)
            {
                Console.WriteLine("Запрос оплаты завершился успешно.");
            }
            else
            {
                HandleHttpResponseError(paymentResponse);
                Console.WriteLine(
                    "Ошибка ожидаема, если на сервере включена искусственная ошибка оплаты."
                );
            }

            Console.WriteLine("\n3. Повторное получение бронирований для проверки статуса...");
            using var verificationResponse = httpClient.GetAsync("api/bookings").Result;

            if (!verificationResponse.IsSuccessStatusCode)
            {
                HandleHttpResponseError(verificationResponse);
                return;
            }

            var updatedBookings = verificationResponse
                .Content.ReadFromJsonAsync<List<Booking>>(jsonOptions)
                .Result;

            var updatedBooking = updatedBookings?.FirstOrDefault(b => b.Id == bookingId);

            if (updatedBooking == null)
            {
                Console.WriteLine("После операции бронирование не найдено.");
                return;
            }

            Console.WriteLine("----------------------------------------");
            Console.WriteLine($"GUID: {updatedBooking.Id}");
            Console.WriteLine($"Статус после запроса: {FormatStatus(updatedBooking.Status)}");

            if (updatedBooking.Status == BookingStatus.Paid)
            {
                Console.WriteLine("Статус изменён на Paid.");
            }
            else
            {
                Console.WriteLine("Статус бронирования не изменился.");
            }

            Console.WriteLine("----------------------------------------");
        }
        catch (Exception ex)
        {
            HandleException(ex, "эксперименте оплаты бронирования");
        }
    }

    internal static void CancelBooking()
    {
        Console.WriteLine("\n ОТМЕНА БРОНИРОВАНИЯ:");
        Console.Write("Введите GUID бронирования для отмены: ");

        if (!Guid.TryParse(Console.ReadLine(), out var bookingId))
        {
            Console.WriteLine("Некорректные входные данные: неверный формат GUID.");
            return;
        }

        Console.Write("Укажите причину отмены: ");
        var reason = Console.ReadLine()?.Trim() ?? "Без указания причины";

        try
        {
            var dto = new CancelBookingRequest(reason);

            var response = httpClient
                .PostAsJsonAsync($"api/bookings/{bookingId}/cancel", dto)
                .Result;

            if (response.IsSuccessStatusCode)
            {
                var result = response
                    .Content.ReadFromJsonAsync<BookingCancelledEvent>(jsonOptions)
                    .Result;

                Console.WriteLine("Бронирование успешно отменено!");

                if (result != null)
                {
                    Console.WriteLine($"Причина: {result.Reason}");
                }
            }
            else
            {
                HandleHttpResponseError(response);
            }
        }
        catch (Exception ex)
        {
            HandleException(ex, "отмене бронирования");
        }
    }

    internal static string FormatStatus(BookingStatus status) =>
        status switch
        {
            BookingStatus.Created => "[Создано]",
            BookingStatus.Paid => "[Оплачено]",
            BookingStatus.Cancelled => "[Отменено]",
            _ => $"[{status}]",
        };
}

public enum BookingStatus
{
    Created = 0,
    Paid = 1,
    Cancelled = 2,
}

public class Room
{
    public Guid Id { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public decimal PricePerNight { get; set; }
}

public class Guest
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
}

public class Booking
{
    public Guid Id { get; set; }
    public Guid GuestId { get; set; }
    public Guid RoomId { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public decimal TotalPrice { get; set; }
    public BookingStatus Status { get; set; }
}

public record CreateGuestRequest(string FullName);

public record CreateBookingRequest(
    Guid GuestId,
    Guid RoomId,
    DateTime CheckInDate,
    DateTime CheckOutDate
);

public record PayBookingRequest(decimal Amount);

public record PayBookingResponse(object Payment, object PaymentEvent, decimal? Change);

public record CancelBookingRequest(string Reason);

public record BookingCancelledEvent(Guid BookingId, string Reason, DateTime CancelledAt);

public record AvailabilityResponse(bool IsAvailable);

public record CreateBookingResponse(Booking Booking);

public record ErrorResponse(string Message);
