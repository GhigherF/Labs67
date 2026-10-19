using System.Text.Json;
using System.Web;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

const string CookieKey = "client_state";
AppState GetState(HttpContext ctx)
{
    if (ctx.Request.Cookies.TryGetValue(CookieKey, out var rawCookie) && !string.IsNullOrEmpty(rawCookie))
    {
        try
        {
            var json = HttpUtility.UrlDecode(rawCookie);
            return JsonSerializer.Deserialize<AppState>(json) ?? new AppState();
        }
        catch { }
    }
    return new AppState();
}

void SaveState(HttpContext ctx, AppState state)
{
    var json = JsonSerializer.Serialize(state);
    ctx.Response.Cookies.Append(CookieKey, HttpUtility.UrlEncode(json), new CookieOptions
    {
        Path = "/",
        HttpOnly = false
    });
}

app.MapGet("/stack", (HttpContext context) =>
{
    var state = GetState(context);
    int top = state.Stack.Count > 0 ? state.Stack[^1] : 0;
    int total = state.Result + top;

    context.Response.Headers.Append("X-Server-Host", Environment.MachineName);
    return Results.Ok(new { result = total });
});

app.MapPost("/stack", async (HttpContext context) =>
{
    var body = await context.Request.ReadFromJsonAsync<ResultDto>();
    if (body == null) return Results.BadRequest();

    var state = GetState(context);
    state.Result = body.Result;
    SaveState(context, state);

    return Results.Ok(new { message = "Base result updated", baseResult = state.Result });
});

app.MapPut("/stack", (HttpContext context, int? ADD) =>
{
    if (!ADD.HasValue) return Results.BadRequest("Query parameter ADD is required.");

    var state = GetState(context);
    state.Stack.Add(ADD.Value);
    SaveState(context, state);

    return Results.Ok(new { message = "Value pushed", value = ADD.Value });
});

app.MapDelete("/stack", (HttpContext context) =>
{
    var state = GetState(context);
    if (state.Stack.Count == 0) return Results.BadRequest("Stack is empty.");

    int popped = state.Stack[^1];
    state.Stack.RemoveAt(state.Stack.Count - 1);
    SaveState(context, state);

    return Results.Ok(new { message = "Value popped", value = popped });
});

app.Run("http://0.0.0.0:8080");

public class AppState
{
    public int Result { get; set; } = 0;
    public List<int> Stack { get; set; } = new();
}

public record ResultDto(int Result);