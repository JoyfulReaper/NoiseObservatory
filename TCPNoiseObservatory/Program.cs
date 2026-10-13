using TCPNoiseObservatory.Configuration;
using TCPNoiseObservatory.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddCors(options => options.AddPolicy("LabSnapshot", policy =>
    policy.WithOrigins("https://kgivler.com", "https://www.kgivler.com")
        .WithMethods("GET")));

builder.Services.Configure<TcpNoiseOptions>(
    builder.Configuration.GetSection("TcpNoise"));

builder.Services.Configure<RemoteSnapshotOptions>(
    builder.Configuration.GetSection("RemoteSnapshot"));

var remoteSnapshotUrl = builder.Configuration["RemoteSnapshot:Url"];

if (string.IsNullOrWhiteSpace(remoteSnapshotUrl))
{
    builder.Services.AddSingleton<NoiseEventStore>();
    builder.Services.AddSingleton<INoiseEventStore>(sp =>
        sp.GetRequiredService<NoiseEventStore>());
    builder.Services.AddSingleton<INoiseSnapshotProvider>(sp =>
        sp.GetRequiredService<NoiseEventStore>());
    builder.Services.AddSingleton<NoiseEventSanitizer>();
    builder.Services.AddHostedService<TcpNoiseSubscriber>();
}
else
{
    builder.Services.AddSingleton<RemoteSnapshotCache>();
    builder.Services.AddSingleton<INoiseSnapshotProvider>(sp =>
        sp.GetRequiredService<RemoteSnapshotCache>());
    builder.Services.AddHttpClient("remote-snapshot", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
    });
    builder.Services.AddHostedService<RemoteSnapshotPoller>();
}

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseCors();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "tcpnoise-observatory"
}));

app.MapGet("/api/snapshot", (HttpContext context, INoiseSnapshotProvider snapshots) =>
{
    context.Response.Headers.CacheControl = "no-store";
    return Results.Ok(snapshots.GetSnapshot());
}).RequireCors("LabSnapshot");

app.MapRazorPages();

app.Run();

// Allow the integration tests to host the actual HTTP pipeline.
public partial class Program { }
