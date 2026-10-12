using TCPNoiseObservatory.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddSingleton<FakeNoiseData>();

var app = builder.Build();

app.UseStaticFiles();
app.MapRazorPages();
//app.MapObservatoryEndpoints();

app.Run();
