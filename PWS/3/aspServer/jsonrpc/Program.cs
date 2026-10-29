using System.Text.Json;
using System.Text.Json.Nodes;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "JSONRPCServer is running.");

app.MapPost(
    "/rpc",
    async (HttpRequest request) =>
    {
        JsonNode? body;

        try
        {
            body = await JsonNode.ParseAsync(request.Body);
        }
        catch (JsonException)
        {
            return Results.Json(Error(null, -32700, "Parse error"));
        }

        if (body is JsonArray batch)
        {
            if (batch.Count == 0)
            {
                return Results.Json(Error(null, -32600, "Invalid Request"));
            }

            var responses = new JsonArray();

            foreach (var item in batch)
            {
                var response = ProcessRequest(item);

                if (response != null)
                {
                    responses.Add(response);
                }
            }

            if (responses.Count == 0)
            {
                return Results.NoContent();
            }

            return Results.Json(responses);
        }

        var result = ProcessRequest(body);

        if (result == null)
        {
            return Results.NoContent();
        }

        return Results.Json(result);
    }
);

app.Run("http://localhost:5000");

static JsonObject? ProcessRequest(JsonNode? request)
{
    if (request is not JsonObject obj)
    {
        return Error(null, -32600, "Invalid Request");
    }

    if (
        obj["jsonrpc"]?.GetValue<string>() != "2.0"
        || obj["method"] is not JsonValue methodValue
        || !methodValue.TryGetValue<string>(out var method)
    )
    {
        return Error(null, -32600, "Invalid Request");
    }

    JsonNode? id = obj["id"]?.DeepClone();
    bool hasId = obj.ContainsKey("id");

    if (obj["id"] is JsonObject or JsonArray)
    {
        return Error(null, -32600, "Invalid Request");
    }

    try
    {
        JsonNode? parameters = obj["params"];

        double x;
        double y;
        double result;

        switch (method)
        {
            case "SUM":
            case "SUB":
            case "MUL":
            case "DIV":
                (x, y) = ReadBinaryParameters(parameters);

                result = method switch
                {
                    "SUM" => x + y,
                    "SUB" => x - y,
                    "MUL" => x * y,
                    "DIV" when y == 0 => throw new RpcFault(-32000, "Division by zero"),
                    "DIV" => x / y,
                    _ => throw new RpcFault(-32601, "Method not found"),
                };

                return hasId ? Success(id, JsonValue.Create(result)) : null;

            case "FACT":
                int n = ReadFactorialParameter(parameters);

                if (n < 0)
                {
                    throw new RpcFault(-32000, "Negative factorial argument");
                }

                if (n > 12)
                {
                    throw new RpcFault(-32000, "Factorial exceeds Int32.MaxValue");
                }

                int factorial = 1;

                for (int i = 2; i <= n; i++)
                {
                    factorial *= i;
                }

                return hasId ? Success(id, JsonValue.Create(factorial)) : null;

            default:
                return hasId ? Error(id, -32601, "Method not found") : null;
        }
    }
    catch (RpcFault ex)
    {
        return hasId ? Error(id, ex.Code, ex.Message) : null;
    }
    catch (Exception ex)
        when (ex is FormatException
            || ex is InvalidOperationException
            || ex is ArgumentException
            || ex is OverflowException
            || ex is NullReferenceException
        )
    {
        return hasId ? Error(id, -32602, "Invalid params") : null;
    }
}

static (double x, double y) ReadBinaryParameters(JsonNode? parameters)
{
    if (parameters is JsonArray array && array.Count == 2)
    {
        return (ReadDouble(array[0]), ReadDouble(array[1]));
    }

    if (
        parameters is JsonObject obj
        && obj.Count == 2
        && obj.ContainsKey("x")
        && obj.ContainsKey("y")
    )
    {
        return (ReadDouble(obj["x"]), ReadDouble(obj["y"]));
    }

    throw new RpcFault(-32602, "Expected [x, y] or {x, y}");
}

static double ReadDouble(JsonNode? node)
{
    if (
        node is JsonValue value
        && value.TryGetValue<double>(out var number)
        && double.IsFinite(number)
    )
    {
        return number;
    }

    throw new RpcFault(-32602, "Parameter must be a finite number");
}

static int ReadFactorialParameter(JsonNode? parameters)
{
    JsonNode? value = parameters switch
    {
        JsonArray array when array.Count == 1 => array[0],
        JsonObject obj when obj.Count == 1 && obj.ContainsKey("x") => obj["x"],
        _ => throw new RpcFault(-32602, "Expected [x] or {x}"),
    };

    if (value is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var number))
    {
        return number;
    }

    throw new RpcFault(-32602, "Parameter must be an integer");
}

static JsonObject Success(JsonNode? id, JsonNode? result)
{
    return new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["result"] = result,
        ["id"] = id?.DeepClone(),
    };
}

static JsonObject Error(JsonNode? id, int code, string message)
{
    return new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
        ["id"] = id?.DeepClone(),
    };
}

sealed class RpcFault(int code, string message) : Exception(message)
{
    public int Code { get; } = code;
}
