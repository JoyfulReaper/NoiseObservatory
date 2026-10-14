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

    public string NoiseLevel => Snapshot.LevelDisplay;

    public void OnGet()
    {
        Snapshot = _snapshotProvider.GetSnapshot();
    }
}
