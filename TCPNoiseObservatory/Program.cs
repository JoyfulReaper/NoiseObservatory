using TCPNoiseObservatory.Configuration;
using TCPNoiseObservatory.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.Configure<TcpNoiseOptions>(
    builder.Configuration.GetSection("TcpNoise"));

builder.Services.AddSingleton<INoiseEventStore, NoiseEventStore>();
builder.Services.AddSingleton<NoiseEventSanitizer>();
builder.Services.AddHostedService<TcpNoiseSubscriber>();

var app = builder.Build();

app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "tcpnoise-observatory"
}));

app.MapRazorPages();

app.Run();