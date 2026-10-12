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

    public void OnGet()
    {
        Snapshot = _fakeNoiseData.GetSnapshot();
    }
}