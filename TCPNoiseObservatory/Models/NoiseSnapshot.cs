using System.Text.Json.Serialization;

namespace TCPNoiseObservatory.Models;

public sealed record NoiseSnapshot(
    int EventsLastFiveMinutes,
    int EventsLastHour,
    IReadOnlyList<NoiseEvent> RecentEvents)
{
    [JsonIgnore]
    public NoiseLevel CurrentNoiseLevel => NoiseLevelClassifier.FromFiveMinuteEventCount(EventsLastFiveMinutes);

    [JsonPropertyName("level")]
    public string LevelCode => CurrentNoiseLevel.ToApiValue();

    [JsonPropertyName("levelDisplay")]
    public string LevelDisplay => CurrentNoiseLevel.ToDisplayValue();

    public double EventsPerMinute => EventsLastFiveMinutes / 5d;
}
