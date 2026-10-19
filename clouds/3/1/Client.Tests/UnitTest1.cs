using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Client.Tests
{
    public class ProgramTests
    {
        private readonly ITestOutputHelper _output;

        private const string ServerUrl = "http://localhost:5230/";

        public ProgramTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private HttpClient CreateClient()
        {
            return new HttpClient
            {
                BaseAddress = new Uri(ServerUrl),
                Timeout = TimeSpan.FromSeconds(15),
            };
        }

        [Fact]
        public async Task Test1_GetRooms_SuccessScenario()
        {
            using var client = CreateClient();

            using var response = await client.GetAsync("api/rooms");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var rooms = await response.Content.ReadFromJsonAsync<List<RoomResponse>>();

            Assert.NotNull(rooms);
            Assert.NotEmpty(rooms);

            foreach (var room in rooms)
            {
                _output.WriteLine($"Room number: {room.Number}, Price: {room.Price}");
            }
        }

        [Fact]
        public async Task Test2_UnknownOperation_ReturnsNotFound()
        {
            using var client = CreateClient();

            using var response = await client.GetAsync("api/unknown-operation");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Test3_InvalidGuestData_ReturnsBadRequest()
        {
            using var client = CreateClient();

            var request = new { name = "" };

            using var response = await client.PostAsJsonAsync("api/guests", request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var body = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Response: {body}");

            Assert.False(string.IsNullOrWhiteSpace(body));
        }

        [Fact]
        public async Task Test4_ServerUnavailable_ThrowsHttpRequestException()
        {
            using var client = new HttpClient
            {
                BaseAddress = new Uri("http://127.0.0.1:1/"),
                Timeout = TimeSpan.FromSeconds(3),
            };

            await Assert.ThrowsAnyAsync<HttpRequestException>(async () =>
                await client.GetAsync("api/rooms")
            );
        }

        [Fact]
        public void Test5_ClientClockOffset_IsSevenSeconds()
        {
            var realTime = DateTimeOffset.UtcNow;
            var clientTime = realTime.AddSeconds(7);

            var offset = clientTime - realTime;

            _output.WriteLine($"Real time: {realTime:O}");
            _output.WriteLine($"Client time: {clientTime:O}");
            _output.WriteLine($"Client clock offset: {offset.TotalSeconds:F3} seconds");

            Assert.Equal(7, offset.TotalSeconds);
        }

        [Fact]
        public void Test6_CalculateOffset_ReturnsMinusSevenSeconds()
        {
            var serverTime = new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

            var clientTime = serverTime.AddSeconds(7);

            var offset = serverTime - clientTime;

            _output.WriteLine($"Server time: {serverTime:O}");

            _output.WriteLine($"Client time: {clientTime:O}");

            _output.WriteLine($"Calculated offset: {offset.TotalSeconds:F3} seconds");

            Assert.Equal(-7, offset.TotalSeconds);
        }

        [Fact]
        public void Test7_CorrectedTime_EqualsServerTime()
        {
            var serverTime = DateTimeOffset.UtcNow;

            var clientTime = serverTime.AddSeconds(7);

            var offset = serverTime - clientTime;

            var correctedTime = clientTime + offset;

            _output.WriteLine($"Client time: {clientTime:O}");
            _output.WriteLine($"Server time: {serverTime:O}");
            _output.WriteLine($"Calculated offset: {offset.TotalSeconds:F3} seconds");
            _output.WriteLine($"Corrected time: {correctedTime:O}");

            Assert.Equal(serverTime, correctedTime);
        }

        [Fact]
        public async Task Test8_TimeEndpoint_ReturnsRealServerTime()
        {
            using var client = CreateClient();

            var requestTime = DateTimeOffset.UtcNow;

            var stopwatch = Stopwatch.StartNew();

            using var response = await client.GetAsync("api/time");

            stopwatch.Stop();

            var responseTime = DateTimeOffset.UtcNow;
            var rtt = stopwatch.Elapsed;

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<TimeResponse>();

            Assert.NotNull(result);

            var estimatedNetworkDelay = TimeSpan.FromTicks(rtt.Ticks / 2);

            var clientTime = requestTime;
            var offset = result.ServerTime - clientTime;
            var correctedTime = responseTime + offset;

            _output.WriteLine($"Request time: {requestTime:O}");
            _output.WriteLine($"Response time: {responseTime:O}");
            _output.WriteLine($"Server time: {result.ServerTime:O}");
            _output.WriteLine($"RTT: {rtt.TotalMilliseconds:F2} ms");
            _output.WriteLine(
                $"Estimated network delay: {estimatedNetworkDelay.TotalMilliseconds:F2} ms"
            );
            _output.WriteLine($"Calculated offset: {offset.TotalSeconds:F3} seconds");
            _output.WriteLine($"Corrected time: {correctedTime:O}");

            Assert.True(result.ServerTime > DateTimeOffset.MinValue);
            Assert.True(rtt >= TimeSpan.Zero);
        }

        [Fact]
        public async Task Test9_TimeEndpoint_RecordsResponseDelay()
        {
            using var client = CreateClient();

            var requestTime = DateTimeOffset.UtcNow;

            var stopwatch = Stopwatch.StartNew();

            using var response = await client.GetAsync("api/time");

            stopwatch.Stop();

            var responseTime = DateTimeOffset.UtcNow;

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<TimeResponse>();

            Assert.NotNull(result);

            var rtt = responseTime - requestTime;

            _output.WriteLine($"Request time: {requestTime:O}");
            _output.WriteLine($"Response time: {responseTime:O}");
            _output.WriteLine($"Measured RTT: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
            _output.WriteLine($"Calculated RTT: {rtt.TotalMilliseconds:F2} ms");
            _output.WriteLine($"Server response delay: {result.ResponseDelayMs} ms");

            Assert.True(result.ResponseDelayMs >= 0);
            Assert.True(rtt >= TimeSpan.Zero);
        }

        public sealed class RoomResponse
        {
            public int Number { get; set; }

            public decimal Price { get; set; }
        }

        public sealed class TimeResponse
        {
            public DateTimeOffset ServerTime { get; set; }

            public int ResponseDelayMs { get; set; }
        }
    }
}
