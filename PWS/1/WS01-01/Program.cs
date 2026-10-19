using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

app.UseSession();

app.MapGet(
    "/stack",
    (HttpContext context) =>
    {
        int result = context.Session.GetInt32("RESULT") ?? 0;
        List<int> stack = GetStack(context);

        if (stack.Count > 0)
        {
            result += stack[^1];
        }

        return Results.Json(new { RESULT = result });
    }
);

app.MapPost(
    "/stack",
    async (HttpContext context) =>
    {
        ResultModel? model;

        try
        {
            model = await context.Request.ReadFromJsonAsync<ResultModel>();
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { message = "Некорректный JSON" });
        }

        if (model is null)
        {
            return Results.BadRequest(new { message = "Необходимо передать RESULT" });
        }

        context.Session.SetInt32("RESULT", model.RESULT);

        return Results.Json(new { RESULT = model.RESULT });
    }
);

app.MapPut(
    "/stack",
    (HttpContext context, int ADD) =>
    {
        List<int> stack = GetStack(context);

        stack.Add(ADD);
        SaveStack(context, stack);

        return Results.Ok(new { message = "Элемент добавлен в стек", ADD });
    }
);

app.MapDelete(
    "/stack",
    (HttpContext context) =>
    {
        List<int> stack = GetStack(context);

        if (stack.Count == 0)
        {
            return Results.BadRequest(new { message = "Стек пуст" });
        }

        int result = context.Session.GetInt32("RESULT") ?? 0;
        int top = stack[^1];

        stack.RemoveAt(stack.Count - 1);

        result += top;

        context.Session.SetInt32("RESULT", result);
        SaveStack(context, stack);

        return Results.Json(new { RESULT = result });
    }
);

app.MapFallback(() => Results.NotFound(new { message = "Неверный маршрут" }));

app.Run();

static List<int> GetStack(HttpContext context)
{
    byte[]? data = context.Session.Get("STACK");

    if (data is null)
    {
        return new List<int>();
    }

    return JsonSerializer.Deserialize<List<int>>(data) ?? new List<int>();
}

static void SaveStack(HttpContext context, List<int> stack)
{
    context.Session.Set("STACK", JsonSerializer.SerializeToUtf8Bytes(stack));
}

public class ResultModel
{
    public int RESULT { get; set; }
}
