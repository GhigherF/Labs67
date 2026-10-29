using Grpc.Core;
using gRPCServer01.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(
        5001,
        listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http2;
        }
    );
});

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<CalculatorService>();

app.MapGet("/", () => "gRPCServer01 is running.");

app.Run();
