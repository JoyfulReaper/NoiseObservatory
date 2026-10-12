using TCPNoiseObservatory.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddSingleton<INoiseEventStore, NoiseEventStore>();

var app = builder.Build();

app.UseStaticFiles();
app.MapRazorPages();

app.Run();
