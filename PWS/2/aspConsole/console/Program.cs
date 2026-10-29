using Grpc.Core;
using Grpc.Net.Client;
using gRPCServer01;

using var channel = GrpcChannel.ForAddress("http://localhost:5001");

var client = new Calculator.CalculatorClient(channel);

async Task Call(string name, Func<Task> action)
{
    try
    {
        await action();
    }
    catch (RpcException ex)
    {
        Console.WriteLine($"{name}: ошибка gRPC [{ex.StatusCode}] — {ex.Status.Detail}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{name}: ошибка — {ex.Message}");
    }
}

await Call(
    "SUM",
    async () =>
    {
        var response = await client.SUMAsync(new BinaryRequest { X = 10, Y = 5 });

        Console.WriteLine($"SUM(10, 5) = {response.Result}");
    }
);

await Call(
    "SUB",
    async () =>
    {
        var response = await client.SUBAsync(new BinaryRequest { X = 10, Y = 5 });

        Console.WriteLine($"SUB(10, 5) = {response.Result}");
    }
);

await Call(
    "MUL",
    async () =>
    {
        var response = await client.MULAsync(new BinaryRequest { X = 10, Y = 5 });

        Console.WriteLine($"MUL(10, 5) = {response.Result}");
    }
);

await Call(
    "DIV",
    async () =>
    {
        var response = await client.DIVAsync(new BinaryRequest { X = 10, Y = 5 });

        Console.WriteLine($"DIV(10, 5) = {response.Result}");
    }
);

await Call(
    "FACT",
    async () =>
    {
        var response = await client.FACTAsync(new FactorialRequest { X = 5 });

        Console.WriteLine($"FACT(5) = {response.Result}");
    }
);

Console.WriteLine("\nПроверка обработки исключений:");

await Call(
    "DIV",
    async () =>
    {
        var response = await client.DIVAsync(new BinaryRequest { X = 10, Y = 0 });

        Console.WriteLine($"DIV(10, 0) = {response.Result}");
    }
);

await Call(
    "FACT",
    async () =>
    {
        var response = await client.FACTAsync(new FactorialRequest { X = 13 });

        Console.WriteLine($"FACT(13) = {response.Result}");
    }
);

await Call(
    "FACT",
    async () =>
    {
        var response = await client.FACTAsync(new FactorialRequest { X = -5 });

        Console.WriteLine($"FACT(-5) = {response.Result}");
    }
);

Console.WriteLine("\nВсе вызовы завершены.");
