using TCPNoiseObservatory.Models;

namespace TCPNoiseObservatory.Services;

public sealed class NoiseEventStore : INoiseEventStore
{
    private const int MaxEvents = 1000;
    private readonly Queue<NoiseEvent> _events = new();
    private readonly object _lock = new();

    public void Add(NoiseEvent noiseEvent)
    {
        lock (_lock)
        {
            _events.Enqueue(noiseEvent);

            while (_events.Count > MaxEvents)
            {
                _events.Dequeue();
            }
        }
    }

    public NoiseSnapshot GetSnapshot()
    {
        NoiseEvent[] events;

        lock (_lock)
        {
            events = _events.ToArray();
        }

        var now = DateTimeOffset.UtcNow;
        int eventsLastFiveMin = events.Count(e => e.Timestamp >= now.AddMinutes(-5));
        int eventsLastHour = events.Count(e => e.Timestamp >= now.AddHours(-1));

        return new NoiseSnapshot(eventsLastFiveMin, eventsLastHour, events);
    }
}