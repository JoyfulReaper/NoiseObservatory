using Microsoft.AspNetCore.Mvc.RazorPages;
using TCPNoiseObservatory.Models;
using TCPNoiseObservatory.Services;

namespace TCPNoiseObservatory.Pages;

public sealed class IndexModel : PageModel
{
    private readonly FakeNoiseData _fakeNoiseData;

    public IndexModel(FakeNoiseData fakeNoiseData)
    {
        _fakeNoiseData = fakeNoiseData;
    }

    public NoiseSnapshot Snapshot { get; private set; } = default!;

    public string NoiseLevel => Snapshot.EventsLastFiveMinutes switch
    {
        < 20 => "Quiet",
        < 50 => "Drizzle",
        < 100 => "Rain",
        < 150 => "Heavy Rain",
        _ => "Botnet Thunderstorm"
    };

    public void OnGet()
    {
        Snapshot = _fakeNoiseData.GetSnapshot();
    }
}
