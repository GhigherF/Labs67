using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".WS01_01.Session";
    options.IdleTimeout = TimeSpan.FromSeconds(30);
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

app.UseSession();

const string KeyBaseResult = "BASE_RESULT";
const string KeyStack = "STACK_DATA";

int GetBaseResult(ISession session) => session.GetInt32(KeyBaseResult) ?? 0;
void SetBaseResult(ISession session, int val) => session.SetInt32(KeyBaseResult, val);

Stack<int> GetStack(ISession session)
{
    var json = session.GetString(KeyStack);
    return string.IsNullOrEmpty(json)
        ? new Stack<int>()
        : JsonSerializer.Deserialize<Stack<int>>(json)!;
}

void SaveStack(ISession session, Stack<int> stack)
{
    session.SetString(KeyStack, JsonSerializer.Serialize(stack));
}

app.MapGet(
    "/stack",
    (HttpContext context) =>
    {
        var session = context.Session;
        int baseResult = GetBaseResult(session);
        var stack = GetStack(session);

        int topElement = stack.Count > 0 ? stack.Peek() : 0;
        int totalResult = baseResult + topElement;

        return Results.Ok(new { result = totalResult });
    }
);

app.MapPost(
    "/stack",
    async (HttpContext context) =>
    {
        var body = await context.Request.ReadFromJsonAsync<ResultDto>();
        if (body == null)
            return Results.BadRequest();

        SetBaseResult(context.Session, body.Result);
        return Results.Ok(new { message = "Base result updated", baseResult = body.Result });
    }
);

app.MapPut(
    "/stack",
    (HttpContext context, int? ADD) =>
    {
        if (!ADD.HasValue)
            return Results.BadRequest("Query parameter ADD is required.");

        var stack = GetStack(context.Session);
        stack.Push(ADD.Value);
        SaveStack(context.Session, stack);

        return Results.Ok(new { message = "Value pushed", value = ADD.Value });
    }
);

app.MapDelete(
    "/stack",
    (HttpContext context) =>
    {
        var stack = GetStack(context.Session);
        if (stack.Count == 0)
            return Results.BadRequest("Stack is empty.");

        int popped = stack.Pop();
        SaveStack(context.Session, stack);

        return Results.Ok(new { message = "Value popped", value = popped });
    }
);

app.Run("http://0.0.0.0:8080");

public record ResultDto(int Result);
