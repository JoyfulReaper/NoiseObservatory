using System.Text.Json.Serialization;

namespace TCPNoiseObservatory.Models;

// Oldest first: 59 preceding UTC calendar minutes, then the partial current minute.
public sealed record NoiseHistory
{
    public const int MinuteCount = 60;

    public DateTimeOffset AsOf { get; }
    public IReadOnlyList<int> Counts { get; }

    public NoiseHistory(DateTimeOffset asOf, IReadOnlyList<int> counts)
    {
        if (counts is null || counts.Count != MinuteCount || counts.Any(count => count < 0))
            throw new ArgumentException("History must contain 60 nonnegative minute counts.", nameof(counts));
        if (asOf.UtcTicks < (MinuteCount - 1) * TimeSpan.TicksPerMinute)
            throw new ArgumentOutOfRangeException(nameof(asOf));

        AsOf = asOf.ToUniversalTime();
        Counts = Array.AsReadOnly(counts.ToArray());
    }

    [JsonIgnore]
    public DateTimeOffset FirstMinute => new(
        AsOf.UtcTicks / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute
            - (MinuteCount - 1) * TimeSpan.TicksPerMinute, TimeSpan.Zero);
}
