using TCPNoiseObservatory.Models;

namespace TCPNoiseObservatory.Services;

public sealed class NoiseEventStore : INoiseEventStore, INoiseSnapshotProvider
{
    private const int MaxEvents = 5000;
    private const int MaxRecentEvents = 100;
    private const int FiveMinutesInSeconds = 300;
    private const int HourInSeconds = 3600;
    // The extra slot preserves the inclusive one-hour boundary at whole seconds.
    private readonly CountBucket[] _buckets = new CountBucket[HourInSeconds + 1];
    private readonly Queue<NoiseEvent> _events = new();
    private readonly object _lock = new();
    private readonly TimeProvider _timeProvider;

    public NoiseEventStore(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public void Add(NoiseEvent noiseEvent)
    {
        lock (_lock)
        {
            _events.Enqueue(noiseEvent);

            while (_events.Count > MaxEvents)
            {
                _events.Dequeue();
            }

            var nowTicks = _timeProvider.GetUtcNow().UtcTicks;
            var eventTicks = noiseEvent.Timestamp.UtcTicks;
            // Keep sample behavior unchanged, but never count expired/future events.
            // Future events are ignored for counting rather than queued for later.
            if (eventTicks > nowTicks || eventTicks < nowTicks - TimeSpan.TicksPerHour)
            {
                return;
            }

            var second = eventTicks / TimeSpan.TicksPerSecond;
            ref var bucket = ref _buckets[second % _buckets.Length];
            if (bucket.Second != second)
            {
                bucket = new CountBucket { Second = second };
            }

            // NoiseSnapshot uses int counts. Saturate instead of wrapping negative.
            if (bucket.Count < int.MaxValue)
            {
                bucket.Count++;
            }
        }
    }

    public NoiseSnapshot GetSnapshot()
    {
        NoiseEvent[] events;
        long eventsLastFiveMin = 0;
        long eventsLastHour = 0;

        lock (_lock)
        {
            var nowTicks = _timeProvider.GetUtcNow().UtcTicks;
            var currentSecond = nowTicks / TimeSpan.TicksPerSecond;
            // Exclude the partially overlapping oldest second so expired events
            // cannot count. Events can expire less than one second early.
            var firstSecond = currentSecond + (nowTicks % TimeSpan.TicksPerSecond == 0 ? 0 : 1);
            foreach (var bucket in _buckets)
            {
                if (bucket.Second > currentSecond || bucket.Second < firstSecond - HourInSeconds)
                {
                    continue;
                }

                eventsLastHour += bucket.Count;
                if (bucket.Second >= firstSecond - FiveMinutesInSeconds)
                {
                    eventsLastFiveMin += bucket.Count;
                }
            }

            events = _events.ToArray();
        }

        var recentEvents = events
            .OrderByDescending(e => e.Timestamp)
            .ThenByDescending(e => e.SeenCount)
            .Take(MaxRecentEvents)
            .ToArray();

        return new NoiseSnapshot(
            (int)Math.Min(eventsLastFiveMin, int.MaxValue),
            (int)Math.Min(eventsLastHour, int.MaxValue),
            recentEvents);
    }

    private struct CountBucket
    {
        public long Second;
        public int Count;
    }
}
