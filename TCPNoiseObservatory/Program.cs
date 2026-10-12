using TCPNoiseObservatory.Services;
using TCPNoiseObservatory.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<FakeNoiseData>();
builder.Services.AddSingleton<ObservatoryHtmlRenderer>();

var app = builder.Build();

app.MapObservatoryEndpoints();

app.Run();
