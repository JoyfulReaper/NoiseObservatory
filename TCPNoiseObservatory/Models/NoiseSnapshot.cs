namespace TCPNoiseObservatory.Models;

public sealed record NoiseSnapshot(
    int EventsLastFiveMinutes,
    int EventsLastHour,
    IReadOnlyList<NoiseEvent> RecentEvents);
