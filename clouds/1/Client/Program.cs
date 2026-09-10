using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace Client;

class Program
{
    private static readonly HttpClient httpClient = new HttpClient
    {
        BaseAddress = new Uri("http://localhost:5230/"),
        Timeout = TimeSpan.FromSeconds(3),
    };

    private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
    };

    static void Main(string[] args)
    {
        while (true)
        {
            Console.WriteLine("=== СИСТЕМА УПРАВЛЕНИЯ ОТЕЛЕМ ===");
            Console.WriteLine("1. Посмотреть список номеров");
            Console.WriteLine("2. Посмотреть список гостей");
            Console.WriteLine("3. Добавить нового гостя");
            Console.WriteLine("4. Проверить доступность номера");
            Console.WriteLine("5. Создать бронирование");
            Console.WriteLine("6. Посмотреть список бронирований");
            Console.WriteLine("7. Оплатить бронирование");
            Console.WriteLine("8. Отменить бронирование");
            Console.WriteLine("9. Запустить авто-тестирование (Требование 9)");
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
                        RunAutomatedTests();
                        break;
                    case "0":
                        return;
                    default:
                        Console.WriteLine(
                            "[!] Некорректная операция: указан неизвестный пункт меню."
                        );
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

    // ==========================================
    // ТРЕБОВАНИЕ 8: ЦЕНТРАЛИЗОВАННАЯ ОБРАБОТКА ОШИБОК
    // ==========================================

    private static void HandleException(Exception ex, string context = "")
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
                        $"[!] Ошибка: Сервер недоступен или отсутствует сетевое подключение ({context})."
                    );
                }
                else
                {
                    Console.WriteLine(
                        $"[!] Ошибка сетевого взаимодействия HTTP (Статус: {httpEx.StatusCode})."
                    );
                }
                break;

            case TaskCanceledException:
                Console.WriteLine(
                    $"[!] Ошибка: Превышено время ожидания ответа от сервера (Timeout)."
                );
                break;

            case FormatException:
                Console.WriteLine(
                    $"[!] Некорректные входные данные: неверный формат числа, даты или GUID."
                );
                break;

            default:
                Console.WriteLine(
                    $"[!] Произошла непредвиденная ошибка ({context}): {targetEx.Message}"
                );
                break;
        }
    }

    private static void HandleHttpResponseError(HttpResponseMessage response)
    {
        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                Console.WriteLine(
                    "[!] Ошибка (404): Запрашиваемый объект или эндпоинт не найден на сервере."
                );
                break;
            case HttpStatusCode.BadRequest:
                var error = TryReadErrorResponse(response);
                Console.WriteLine(
                    $"[!] Ошибка (400 Bad Request): {error ?? "Переданы некорректные входные данные или выполнена недопустимая операция."}"
                );
                break;
            case HttpStatusCode.InternalServerError:
                Console.WriteLine("[!] Внутренняя ошибка сервера (500).");
                break;
            default:
                Console.WriteLine(
                    $"[!] Ошибка ответа сервера: {(int)response.StatusCode} {response.ReasonPhrase}"
                );
                break;
        }
    }

    private static string? TryReadErrorResponse(HttpResponseMessage response)
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

    // ==========================================
    // ТРЕБОВАНИЕ 9: АВТОМАТИЧЕСКОЕ ТЕСТИРОВАНИЕ
    // ==========================================

    private static void RunAutomatedTests()
    {
        Console.WriteLine("\n==========================================");
        Console.WriteLine("   ЗАПУСК АВТОМАТИЧЕСКИХ ТЕСТОВ SYSTEM");
        Console.WriteLine("==========================================");

        // Тест 1: Успешный сценарий
        Console.WriteLine("\n[ТЕСТ 1] Успешный сценарий: Запрос списка номеров");
        try
        {
            var response = httpClient.GetAsync("api/rooms").Result;
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine(" -> РЕЗУЛЬТАТ: УСПЕШНО (200 OK, данные получены)");
            }
            else
            {
                Console.WriteLine($" -> РЕЗУЛЬТАТ: ПРОВАЛ (Сервер вернул {response.StatusCode})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $" -> РЕЗУЛЬТАТ: ПРОВАЛ ({ex.InnerException?.Message ?? ex.Message})"
            );
        }

        // Тест 2: Неизвестная операция / Отсутствующий маршрут
        Console.WriteLine(
            "\n[ТЕСТ 2] Неизвестная операция: Запрос к несуществующему URL (api/unknown-endpoint)"
        );
        try
        {
            var response = httpClient.GetAsync("api/unknown-endpoint").Result;
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                Console.WriteLine(
                    " -> РЕЗУЛЬТАТ: УСПЕШНО (Ожидаемая ошибка 404 Not Found корректно обработана)"
                );
            }
            else
            {
                Console.WriteLine(
                    $" -> РЕЗУЛЬТАТ: ПРОВАЛ (Получен неожиданный статус {response.StatusCode})"
                );
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($" -> РЕЗУЛЬТАТ: ПРОВАЛ ({ex.Message})");
        }

        // Тест 3: Некорректные данные
        Console.WriteLine("\n[ТЕСТ 3] Некорректные данные: Попытка создания гостя с пустым именем");
        try
        {
            var dto = new CreateGuestRequest("");
            var response = httpClient.PostAsJsonAsync("api/guests", dto).Result;
            if (response.StatusCode == HttpStatusCode.BadRequest || !response.IsSuccessStatusCode)
            {
                Console.WriteLine(
                    $" -> РЕЗУЛЬТАТ: УСПЕШНО (Сервер вернул статус {response.StatusCode} в ответ на некорректный ввод)"
                );
            }
            else
            {
                Console.WriteLine(" -> РЕЗУЛЬТАТ: ПРОВАЛ (Сервер принял пустые данные)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($" -> РЕЗУЛЬТАТ: ИСКЛЮЧЕНИЕ ({ex.Message})");
        }

        // Тест 4: Недоступный сервер
        Console.WriteLine(
            "\n[ТЕСТ 4] Недоступный сервер: Запрос к заблокированному/несуществующему порту (http://localhost:59999/)"
        );
        try
        {
            using var invalidClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var response = invalidClient.GetAsync("http://localhost:59999/api/rooms").Result;
            Console.WriteLine(" -> РЕЗУЛЬТАТ: ПРОВАЛ (Запрос почему-то выполнился)");
        }
        catch (Exception ex)
        {
            var inner = ex is AggregateException agg ? agg.InnerException : ex;
            Console.WriteLine(
                $" -> РЕЗУЛЬТАТ: УСПЕШНО (Перехвачена ошибка недоступности сервера: {inner?.Message})"
            );
        }

        Console.WriteLine("\n==========================================");
        Console.WriteLine("   ТЕСТИРОВАНИЕ ЗАВЕРШЕНО");
        Console.WriteLine("==========================================");
    }

    // ==========================================
    // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
    // ==========================================

    private static List<Room>? GetRooms()
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

    private static List<Guest>? GetGuests()
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

    private static Room? FindRoomByNumber(IEnumerable<Room> rooms, string inputNumber)
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

    private static Guest? FindGuestByNameOrIndex(IEnumerable<Guest> guests, string input)
    {
        var clean = input.Trim();
        if (string.IsNullOrWhiteSpace(clean))
            return null;

        return guests.FirstOrDefault(g =>
            g.FullName.Equals(clean, StringComparison.OrdinalIgnoreCase)
            || g.FullName.Contains(clean, StringComparison.OrdinalIgnoreCase)
        );
    }

    // ==========================================
    // ОСНОВНЫЕ МЕТОДЫ ВЗАИМОДЕЙСТВИЯ
    // ==========================================

    private static void ShowRooms()
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

    private static void ShowGuests()
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

    private static void CreateGuest()
    {
        Console.WriteLine("\n ДОБАВЛЕНИЕ НОВОГО ГОСТЯ:");
        Console.Write("Введите ФИО гостя: ");
        var fullName = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(fullName))
        {
            Console.WriteLine("[!] Некорректные входные данные: ФИО не может быть пустым.");
            return;
        }

        try
        {
            var dto = new CreateGuestRequest(fullName);
            var response = httpClient.PostAsJsonAsync("api/guests", dto).Result;

            if (response.IsSuccessStatusCode)
            {
                var createdGuest = response.Content.ReadFromJsonAsync<Guest>(jsonOptions).Result;
                Console.WriteLine($"[✓] Гость успешно добавлен!");
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

    private static void CheckAvailability()
    {
        var rooms = GetRooms();
        if (rooms == null || rooms.Count == 0)
            return;

        Console.Write("Введите номер комнаты (например, 101): ");
        var roomInput = Console.ReadLine() ?? "";

        var room = FindRoomByNumber(rooms, roomInput);
        if (room == null)
        {
            Console.WriteLine($"[!] Отсутствующий объект: Номер '{roomInput}' не найден.");
            return;
        }

        Console.Write("Дата заезда (ГГГГ-ММ-ДД): ");
        if (!DateTime.TryParse(Console.ReadLine(), out var checkIn))
        {
            Console.WriteLine("[!] Некорректные входные данные: некорректный формат даты заезда.");
            return;
        }

        Console.Write("Дата выезда (ГГГГ-ММ-ДД): ");
        if (!DateTime.TryParse(Console.ReadLine(), out var checkOut))
        {
            Console.WriteLine("[!] Некорректные входные данные: некорректный формат даты выезда.");
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
                    Console.WriteLine($"[✓] Номер {room.RoomNumber} СВОБОДЕН на указанные даты.");
                }
                else
                {
                    Console.WriteLine($"[X] Номер {room.RoomNumber} ЗАНЯТ на указанные даты.");
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

    private static void CreateBooking()
    {
        var rooms = GetRooms();
        var guests = GetGuests();

        if (rooms == null || rooms.Count == 0 || guests == null || guests.Count == 0)
            return;

        Console.Write("Введите номер комнаты (например, 101): ");
        var roomInput = Console.ReadLine() ?? "";
        var room = FindRoomByNumber(rooms, roomInput);

        if (room == null)
        {
            Console.WriteLine($"[!] Отсутствующий объект: Номер '{roomInput}' не найден.");
            return;
        }

        Console.Write("Введите ФИО или часть имени гостя: ");
        var guestInput = Console.ReadLine() ?? "";
        var guest = FindGuestByNameOrIndex(guests, guestInput);

        if (guest == null)
        {
            Console.WriteLine($"[!] Отсутствующий объект: Гость '{guestInput}' не найден.");
            return;
        }

        Console.Write("Дата заезда (ГГГГ-ММ-ДД): ");
        if (!DateTime.TryParse(Console.ReadLine(), out var checkIn))
        {
            Console.WriteLine("[!] Некорректные входные данные: формат даты.");
            return;
        }

        Console.Write("Дата выезда (ГГГГ-ММ-ДД): ");
        if (!DateTime.TryParse(Console.ReadLine(), out var checkOut))
        {
            Console.WriteLine("[!] Некорректные входные данные: формат даты.");
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
                Console.WriteLine($"[✓] Бронирование успешно создано!");
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

    private static void ShowBookings()
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

    private static void PayBooking()
    {
        Console.WriteLine("\n ОПЛАТА БРОНИРОВАНИЯ:");
        Console.Write("Введите GUID бронирования для оплаты: ");
        if (!Guid.TryParse(Console.ReadLine(), out var bookingId))
        {
            Console.WriteLine("[!] Некорректные входные данные: неверный формат GUID.");
            return;
        }

        Console.Write("Введите сумму оплаты: ");
        if (!decimal.TryParse(Console.ReadLine(), out var amount))
        {
            Console.WriteLine(
                "[!] Некорректные входные данные: некорректное числовое значение суммы."
            );
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
                Console.WriteLine("[✓] Оплата прошла успешно!");

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

    private static void CancelBooking()
    {
        Console.WriteLine("\n ОТМЕНА БРОНИРОВАНИЯ:");
        Console.Write("Введите GUID бронирования для отмены: ");
        if (!Guid.TryParse(Console.ReadLine(), out var bookingId))
        {
            Console.WriteLine("[!] Некорректные входные данные: неверный формат GUID.");
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
                Console.WriteLine("[✓] Бронирование успешно отменено!");
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

    private static string FormatStatus(BookingStatus status) =>
        status switch
        {
            BookingStatus.Created => "[Создано]",
            BookingStatus.Paid => "[Оплачено]",
            BookingStatus.Cancelled => "[Отменено]",
            _ => $"[{status}]",
        };
}

// ==========================================
// МОДЕЛИ И DTO
// ==========================================

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
