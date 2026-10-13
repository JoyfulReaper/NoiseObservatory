using TCPNoiseObservatory.Models;

namespace TCPNoiseObservatory.Services;

public sealed class RemoteSnapshotCache : INoiseSnapshotProvider
{
    private NoiseSnapshot _snapshot = new(
        EventsLastFiveMinutes: 0,
        EventsLastHour: 0,
        RecentEvents: Array.Empty<NoiseEvent>());

    public NoiseSnapshot GetSnapshot()
    {
        return Volatile.Read(ref _snapshot);
    }

    public void Update(NoiseSnapshot snapshot)
    {
        Volatile.Write(ref _snapshot, snapshot);
    }
}
