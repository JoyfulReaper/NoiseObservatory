using Microsoft.AspNetCore.Mvc.RazorPages;
using TCPNoiseObservatory.Models;
using TCPNoiseObservatory.Services;

namespace TCPNoiseObservatory.Pages;

public sealed class IndexModel : PageModel
{
    private readonly INoiseSnapshotProvider _snapshotProvider;

    public IndexModel(INoiseSnapshotProvider snapshotProvider)
    {
        _snapshotProvider = snapshotProvider;
    }

    public NoiseSnapshot Snapshot { get; private set; } = default!;

    public string NoiseLevel => Snapshot.EventsLastFiveMinutes switch
    {
        < 35 => "Quiet",
        < 50 => "Drizzle",
        < 175 => "Rain",
        < 325 => "Heavy Rain",
        _ => "Botnet Thunderstorm"
    };

    public void OnGet()
    {
        Snapshot = _snapshotProvider.GetSnapshot();
    }
}
