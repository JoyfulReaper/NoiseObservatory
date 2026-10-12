using Microsoft.AspNetCore.Mvc.RazorPages;
using TCPNoiseObservatory.Models;
using TCPNoiseObservatory.Services;

namespace TCPNoiseObservatory.Pages;

public sealed class IndexModel : PageModel
{
    private readonly INoiseEventStore _noiseEventStore;

    public IndexModel(INoiseEventStore noiseEventStore)
    {
        _noiseEventStore = noiseEventStore;
    }

    public NoiseSnapshot Snapshot { get; private set; } = default!;

    public string NoiseLevel => Snapshot.EventsLastFiveMinutes switch
    {
        < 30 => "Quiet",
        < 80 => "Drizzle",
        < 150 => "Rain",
        < 450 => "Heavy Rain",
        _ => "Botnet Thunderstorm"
    };

    public void OnGet()
    {
        Snapshot = _noiseEventStore.GetSnapshot();
    }
}
