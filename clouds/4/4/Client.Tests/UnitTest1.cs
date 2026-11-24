using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Client.Tests;

[Collection("HotelBooking API")]
public sealed class ProgramTests : IDisposable
{
    private const string BaseUrl = "http://localhost:5230/";

    private const bool UseTransactions = true;
    private const bool InjectPaymentError = false;

    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public ProgramTests(ITestOutputHelper output)
    {
        _output = output;
        _client = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(15),
        };
    }

    [Fact]
    public async Task Test1_GetRooms_ReturnsRooms()
    {
        _output.WriteLine("TEST 1: GET /api/rooms");

        using var response = await _client.GetAsync("api/rooms");
        var body = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"HTTP: {(int)response.StatusCode}");
        _output.WriteLine(body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);

        foreach (var room in document.RootElement.EnumerateArray())
        {
            var number = GetProperty(room, "number", "roomNumber");
            var price = GetProperty(room, "price", "pricePerNight");

            _output.WriteLine(
                $"Room: {number?.ToString() ?? "unknown"}; "
                    + $"price: {price?.ToString() ?? "unknown"}"
            );
        }
    }

    [Fact]
    public async Task Test2_UnknownEndpoint_Returns404()
    {
        _output.WriteLine("TEST 2: unknown endpoint");

        using var response = await _client.GetAsync("api/endpoint-that-does-not-exist");

        var body = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"HTTP: {(int)response.StatusCode}");
        _output.WriteLine(body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Test3_InvalidGuest_ReturnsBadRequest()
    {
        _output.WriteLine("TEST 3: invalid guest");

        var payload = JsonSerializer.Serialize(new { name = "" });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await _client.PostAsync("api/guests", content);
        var body = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"HTTP: {(int)response.StatusCode}");
        _output.WriteLine(body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Test4_UnavailableServer_ThrowsConnectionError()
    {
        _output.WriteLine("TEST 4: unavailable server");

        using var client = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:59999/"),
            Timeout = TimeSpan.FromSeconds(3),
        };

        var exception = await Record.ExceptionAsync(() => client.GetAsync("api/rooms"));

        _output.WriteLine($"Exception: {exception?.GetType().FullName ?? "none"}");

        Assert.True(
            exception is HttpRequestException or TaskCanceledException,
            $"Expected a connection error or timeout, got: {exception}"
        );
    }

    [Fact]
    public void Test5_SimulatedClientClockOffset_IsSevenSeconds()
    {
        _output.WriteLine("TEST 5: simulated client clock offset +7 seconds");

        var serverTime = DateTimeOffset.UtcNow;
        var clientTime = serverTime.AddSeconds(7);
        var offset = clientTime - serverTime;

        _output.WriteLine($"Server time: {serverTime:O}");
        _output.WriteLine($"Client time: {clientTime:O}");
        _output.WriteLine($"Offset: {offset.TotalSeconds} seconds");

        Assert.Equal(TimeSpan.FromSeconds(7), offset);
    }

    [Fact]
    public void Test6_CalculateNegativeClockOffset()
    {
        _output.WriteLine("TEST 6: simulated client clock offset -7 seconds");

        var serverTime = DateTimeOffset.UtcNow;
        var clientTime = serverTime.AddSeconds(-7);
        var offset = clientTime - serverTime;

        _output.WriteLine($"Server time: {serverTime:O}");
        _output.WriteLine($"Client time: {clientTime:O}");
        _output.WriteLine($"Offset: {offset.TotalSeconds} seconds");

        Assert.Equal(TimeSpan.FromSeconds(-7), offset);
    }

    [Fact]
    public void Test7_CorrectedClientTime_MatchesServerTime()
    {
        _output.WriteLine("TEST 7: client clock correction");

        var serverTime = DateTimeOffset.UtcNow;
        var offset = TimeSpan.FromSeconds(7);
        var clientTime = serverTime.Add(offset);
        var correctedTime = clientTime.Subtract(offset);

        _output.WriteLine($"Server time: {serverTime:O}");
        _output.WriteLine($"Client time: {clientTime:O}");
        _output.WriteLine($"Corrected time: {correctedTime:O}");

        Assert.Equal(serverTime, correctedTime);
    }

    [Fact]
    public async Task Test8_TimeEndpoint_ReturnsServerTime()
    {
        _output.WriteLine("TEST 8: GET /api/time");

        using var response = await _client.GetAsync("api/time");
        var body = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"HTTP: {(int)response.StatusCode}");
        _output.WriteLine(body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = JsonSerializer.Deserialize<TimeResponse>(body, JsonOptions);

        Assert.NotNull(result);
        Assert.NotEqual(default, result.ServerTime);
        Assert.True(result.ResponseDelayMs >= 0);

        _output.WriteLine($"Server time: {result.ServerTime:O}");
        _output.WriteLine($"Response delay: {result.ResponseDelayMs} ms");
    }

    [Fact]
    public async Task Test9_TimeEndpoint_ReportsResponseDelay()
    {
        _output.WriteLine("TEST 9: response delay");

        var startedAt = DateTimeOffset.UtcNow;

        using var response = await _client.GetAsync("api/time");
        var body = await response.Content.ReadAsStringAsync();

        var finishedAt = DateTimeOffset.UtcNow;
        var roundTripMs = (finishedAt - startedAt).TotalMilliseconds;

        _output.WriteLine($"RTT: {roundTripMs:F2} ms");
        _output.WriteLine($"HTTP: {(int)response.StatusCode}");
        _output.WriteLine(body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = JsonSerializer.Deserialize<TimeResponse>(body, JsonOptions);

        Assert.NotNull(result);
        Assert.True(result.ResponseDelayMs >= 0);

        Assert.True(
            result.ResponseDelayMs <= roundTripMs + 1000,
            "Server response delay cannot substantially exceed measured round-trip time."
        );
    }

    [Fact]
    public async Task Test10_Payment_ChangesBookingStatusAccordingToServerBehavior()
    {
        _output.WriteLine("TEST 10: payment changes booking status");

        var booking = await CreateTestBookingAsync();

        _output.WriteLine($"Booking ID: {booking.Id}");
        _output.WriteLine($"Initial status: {booking.Status}");
        _output.WriteLine($"Total price: {booking.TotalPrice}");

        Assert.Equal(0, booking.Status);

        using var response = await PayBookingAsync(booking.Id, booking.TotalPrice);

        var body = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Payment HTTP: {(int)response.StatusCode}");
        _output.WriteLine(body);

        Assert.True(
            response.IsSuccessStatusCode,
            $"Payment failed: HTTP {(int)response.StatusCode}: {body}"
        );

        var updated = await GetBookingAsync(booking.Id);

        _output.WriteLine($"Status after payment: {updated.Status}");

        Assert.Equal(1, updated.Status);
    }

    [Fact]
    public async Task Test11_ConcurrentPayments_RespectServerBehavior()
    {
        _output.WriteLine("TEST 11: concurrent payments");

        var booking = await CreateTestBookingAsync();

        _output.WriteLine($"Booking ID: {booking.Id}");
        _output.WriteLine($"Amount: {booking.TotalPrice}");

        var firstTask = PayBookingAsync(booking.Id, booking.TotalPrice);
        var secondTask = PayBookingAsync(booking.Id, booking.TotalPrice);

        using var firstResponse = await firstTask;
        using var secondResponse = await secondTask;

        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        var secondBody = await secondResponse.Content.ReadAsStringAsync();

        _output.WriteLine($"First: {(int)firstResponse.StatusCode} {firstBody}");
        _output.WriteLine($"Second: {(int)secondResponse.StatusCode} {secondBody}");

        var successCount = new[] { firstResponse, secondResponse }.Count(response =>
            response.IsSuccessStatusCode
        );

        _output.WriteLine($"Successful requests: {successCount}");

        Assert.Equal(1, successCount);

        var updated = await GetBookingAsync(booking.Id);
        Assert.Equal(1, updated.Status);
    }

    [Fact]
    public async Task Test12_Payment_HandlesInjectedError()
    {
        _output.WriteLine("TEST 12: payment error handling");

        var booking = await CreateTestBookingAsync();

        using var response = await PayBookingAsync(booking.Id, booking.TotalPrice);

        var body = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Booking ID: {booking.Id}");
        _output.WriteLine($"HTTP: {(int)response.StatusCode}");
        _output.WriteLine(body);

        if (InjectPaymentError)
        {
            Assert.False(
                response.IsSuccessStatusCode,
                "Payment should fail when the server injects a payment error."
            );

            var updated = await GetBookingAsync(booking.Id);
            Assert.Equal(UseTransactions ? 0 : 1, updated.Status);
        }
        else
        {
            Assert.True(response.IsSuccessStatusCode, $"Payment unexpectedly failed: {body}");

            var updated = await GetBookingAsync(booking.Id);
            Assert.Equal(1, updated.Status);
        }
    }

    [Fact]
    public async Task Test13_PaymentFailure_RespectsTransactionConfiguration()
    {
        _output.WriteLine("TEST 13: transaction behavior");

        var booking = await CreateTestBookingAsync();

        _output.WriteLine($"Booking ID: {booking.Id}");
        _output.WriteLine($"Initial status: {booking.Status}");
        _output.WriteLine($"UseTransactions: {UseTransactions}");
        _output.WriteLine($"InjectPaymentError: {InjectPaymentError}");

        using var response = await PayBookingAsync(booking.Id, booking.TotalPrice);

        var body = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"HTTP: {(int)response.StatusCode}");
        _output.WriteLine(body);

        var updated = await GetBookingAsync(booking.Id);

        _output.WriteLine($"Status after payment: {updated.Status}");

        if (InjectPaymentError)
        {
            Assert.False(response.IsSuccessStatusCode);

            if (UseTransactions)
            {
                Assert.Equal(0, updated.Status);
            }
            else
            {
                Assert.Equal(1, updated.Status);
            }
        }
        else
        {
            Assert.True(response.IsSuccessStatusCode, $"Payment failed unexpectedly: {body}");

            Assert.Equal(1, updated.Status);
        }
    }

    private async Task<BookingResponse> CreateTestBookingAsync()
    {
        var roomsBody = await _client.GetStringAsync("api/rooms");
        var guestsBody = await _client.GetStringAsync("api/guests");

        using var roomsDocument = JsonDocument.Parse(roomsBody);
        using var guestsDocument = JsonDocument.Parse(guestsBody);

        var rooms = roomsDocument.RootElement.EnumerateArray().ToList();

        var guests = guestsDocument.RootElement.EnumerateArray().ToList();

        Assert.NotEmpty(rooms);
        Assert.NotEmpty(guests);

        var guestId = ReadGuid(guests[0], "id", "guestId");

        for (var dayOffset = 1; dayOffset < 730; dayOffset++)
        {
            var checkIn = DateTime.UtcNow.Date.AddDays(dayOffset);
            var checkOut = checkIn.AddDays(1);

            foreach (var room in rooms)
            {
                var roomId = ReadGuid(room, "id", "roomId");

                var request = new
                {
                    GuestId = guestId,
                    RoomId = roomId,
                    CheckInDate = checkIn,
                    CheckOutDate = checkOut,
                };

                var payload = JsonSerializer.Serialize(request);

                using var content = new StringContent(payload, Encoding.UTF8, "application/json");

                using var response = await _client.PostAsync("api/bookings", content);

                var body = await response.Content.ReadAsStringAsync();

                _output.WriteLine(
                    $"Create booking: room={roomId}, "
                        + $"check-in={checkIn:yyyy-MM-dd}, "
                        + $"check-out={checkOut:yyyy-MM-dd}, "
                        + $"HTTP={(int)response.StatusCode}"
                );

                if (response.IsSuccessStatusCode)
                {
                    using var document = JsonDocument.Parse(body);
                    var root = document.RootElement;

                    var bookingElement = TryGetProperty(root, "booking", out var nested)
                        ? nested
                        : root;

                    var booking = JsonSerializer.Deserialize<BookingResponse>(
                        bookingElement.GetRawText(),
                        JsonOptions
                    );

                    Assert.NotNull(booking);
                    Assert.NotEqual(Guid.Empty, booking.Id);

                    _output.WriteLine(
                        $"Created booking: {booking.Id}, "
                            + $"status={booking.Status}, "
                            + $"price={booking.TotalPrice}"
                    );

                    return booking;
                }

                _output.WriteLine($"Response: {body}");

                var roomOccupied =
                    response.StatusCode == HttpStatusCode.BadRequest
                    && body.Contains("занят", StringComparison.OrdinalIgnoreCase);

                if (roomOccupied)
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"Could not create booking. HTTP {(int)response.StatusCode}: {body}"
                );
            }
        }

        throw new InvalidOperationException("No available room/date combination was found.");
    }

    private async Task<BookingResponse> GetBookingAsync(Guid id)
    {
        var bookings = await GetBookingsAsync();
        var booking = bookings.FirstOrDefault(item => item.Id == id);

        Assert.NotNull(booking);

        return booking;
    }

    private async Task<List<BookingResponse>> GetBookingsAsync()
    {
        using var response = await _client.GetAsync("api/bookings");
        var body = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"GET bookings HTTP: {(int)response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var bookings = JsonSerializer.Deserialize<List<BookingResponse>>(body, JsonOptions);

        Assert.NotNull(bookings);

        return bookings;
    }

    private Task<HttpResponseMessage> PayBookingAsync(Guid bookingId, decimal amount)
    {
        var payload = JsonSerializer.Serialize(new PayBookingRequest(amount));

        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        return _client.PostAsync($"api/bookings/{bookingId}/pay", content);
    }

    private static Guid ReadGuid(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (
                TryGetProperty(element, name, out var value)
                && value.ValueKind == JsonValueKind.String
                && Guid.TryParse(value.GetString(), out var id)
            )
            {
                return id;
            }
        }

        throw new InvalidOperationException($"GUID property not found: {string.Join(", ", names)}");
    }

    private static JsonElement? GetProperty(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(element, name, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    private sealed record PayBookingRequest(decimal Amount);

    private sealed class BookingResponse
    {
        public Guid Id { get; set; }
        public Guid GuestId { get; set; }
        public Guid RoomId { get; set; }
        public int Status { get; set; }
        public decimal TotalPrice { get; set; }
    }

    private sealed class TimeResponse
    {
        public DateTimeOffset ServerTime { get; set; }
        public int ResponseDelayMs { get; set; }
    }
}

[CollectionDefinition("HotelBooking API", DisableParallelization = true)]
public sealed class HotelBookingApiCollection { }
