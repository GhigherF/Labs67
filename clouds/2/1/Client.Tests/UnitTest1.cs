using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Client;
using Xunit;
using Xunit.Abstractions;

namespace Client.Tests
{
    public class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult(_handler(request));
        }
    }

    public class ProgramTests
    {
        private readonly ITestOutputHelper _output;

        public ProgramTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Test1_GetRooms_SuccessScenario()
        {
            _output.WriteLine("=== ТЕСТ 1: Успешный сценарий (Получение списка номеров) ===");
            _output.WriteLine("[Arrange] Подготовка данных и заглушки сервера...");

            var expectedRooms = new List<Room>
            {
                new Room
                {
                    Id = Guid.NewGuid(),
                    RoomNumber = "101",
                    PricePerNight = 1000m,
                },
                new Room
                {
                    Id = Guid.NewGuid(),
                    RoomNumber = "102",
                    PricePerNight = 2000m,
                },
            };

            var handler = new MockHttpMessageHandler(req =>
            {
                _output.WriteLine(
                    $"[Server Mock] Перехвачен запрос: {req.Method} {req.RequestUri}"
                );
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(expectedRooms),
                };
            });

            var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:5230/"),
            };

            _output.WriteLine("[Act] Отправка HTTP-запроса api/rooms...");
            var response = client.GetAsync("api/rooms").Result;
            var rooms = response.Content.ReadFromJsonAsync<List<Room>>().Result;

            _output.WriteLine(
                $"[Response] Статус ответа: {(int)response.StatusCode} {response.StatusCode}"
            );
            _output.WriteLine($"[Response] Получено элементов: {rooms?.Count ?? 0}");

            _output.WriteLine("[Assert] Проверка полученных данных...");
            Assert.True(response.IsSuccessStatusCode);
            Assert.NotNull(rooms);
            Assert.Equal(2, rooms.Count);
            Assert.Equal("101", rooms[0].RoomNumber);

            _output.WriteLine(">>> ТЕСТ 1 УСПЕШНО ПРОЙДЕН <<<\n");
        }

        [Fact]
        public void Test2_UnknownOperation_ReturnsNotFound()
        {
            _output.WriteLine("=== ТЕСТ 2: Неизвестная операция (404 Not Found) ===");
            _output.WriteLine("[Arrange] Настройка эмуляции несуществующего эндпоинта...");

            var handler = new MockHttpMessageHandler(req =>
            {
                _output.WriteLine(
                    $"[Server Mock] Запрос к неизвестному ресурсу: {req.Method} {req.RequestUri}"
                );
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:5230/"),
            };

            _output.WriteLine("[Act] Запрос несуществующего маршрута api/unknown-operation...");
            var response = client.GetAsync("api/unknown-operation").Result;

            _output.WriteLine(
                $"[Response] Код ответа сервера: {(int)response.StatusCode} {response.StatusCode}"
            );

            _output.WriteLine("[Assert] Проверка, что сервер вернул 404...");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.False(response.IsSuccessStatusCode);

            _output.WriteLine(">>> ТЕСТ 2 УСПЕШНО ПРОЙДЕН <<<\n");
        }

        [Fact]
        public void Test3_InvalidData_ReturnsBadRequest()
        {
            _output.WriteLine("=== ТЕСТ 3: Некорректные данные (400 Bad Request) ===");
            _output.WriteLine("[Arrange] Формирование некорректных данных (пустое имя)...");

            var errorPayload = new ErrorResponse("Имя гостя не может быть пустым.");

            var handler = new MockHttpMessageHandler(req =>
            {
                _output.WriteLine(
                    $"[Server Mock] Валидация отклонила данные. Формирование 400 Bad Request."
                );
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = JsonContent.Create(errorPayload),
                };
            });

            var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:5230/"),
            };
            var invalidDto = new CreateGuestRequest("");

            _output.WriteLine("[Act] Отправка POST-запроса api/guests с пустыми данными...");
            var response = client.PostAsJsonAsync("api/guests", invalidDto).Result;
            var error = response.Content.ReadFromJsonAsync<ErrorResponse>().Result;

            _output.WriteLine(
                $"[Response] Статус ответа: {(int)response.StatusCode} {response.StatusCode}"
            );
            _output.WriteLine($"[Response] Сообщение об ошибке от сервера: \"{error?.Message}\"");

            _output.WriteLine("[Assert] Проверка статуса 400 и сообщения об ошибке...");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.NotNull(error);
            Assert.Equal("Имя гостя не может быть пустым.", error.Message);

            _output.WriteLine(">>> ТЕСТ 3 УСПЕШНО ПРОЙДЕН <<<\n");
        }

        [Fact]
        public void Test4_ServerUnavailable_ThrowsHttpRequestException()
        {
            _output.WriteLine("=== ТЕСТ 4: Недоступный сервер (Исключение сети) ===");
            _output.WriteLine("[Arrange] Эмуляция падения сети или отключенного сервера...");

            var handler = new MockHttpMessageHandler(req =>
            {
                _output.WriteLine("[Server Mock] Эмуляция обрыва соединения (SocketException)...");
                throw new HttpRequestException(
                    "Server unavailable",
                    new System.Net.Sockets.SocketException()
                );
            });

            var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:5230/"),
            };

            _output.WriteLine(
                "[Act & Assert] Ожидаем генерацию исключения HttpRequestException..."
            );

            var exception = Assert.Throws<HttpRequestException>(() =>
            {
                try
                {
                    _ = client.GetAsync("api/rooms").Result;
                }
                catch (AggregateException ex)
                {
                    throw ex.InnerException!;
                }
            });

            _output.WriteLine($"[Caught Exception] Поймано исключение: {exception.GetType().Name}");
            _output.WriteLine($"[Caught Exception] Текст ошибки: {exception.Message}");

            _output.WriteLine(">>> ТЕСТ 4 УСПЕШНО ПРОЙДЕН <<<\n");
        }
    }
}
