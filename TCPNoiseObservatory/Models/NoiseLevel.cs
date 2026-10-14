namespace TCPNoiseObservatory.Models;

public enum NoiseLevel
{
    Quiet,
    Drizzle,
    Rain,
    HeavyRain,
    BotnetThunderstorm,
    BotnetHurricane
}

public static class NoiseLevelClassifier
{
    public static NoiseLevel FromFiveMinuteEventCount(int eventCount) => eventCount switch
    {
        < 35 => NoiseLevel.Quiet,
        < 50 => NoiseLevel.Drizzle,
        < 175 => NoiseLevel.Rain,
        < 325 => NoiseLevel.HeavyRain,
        < 500 => NoiseLevel.BotnetThunderstorm,
        _ => NoiseLevel.BotnetHurricane
    };

    public static string ToApiValue(this NoiseLevel level) => level switch
    {
        NoiseLevel.Quiet => "quiet",
        NoiseLevel.Drizzle => "drizzle",
        NoiseLevel.Rain => "rain",
        NoiseLevel.HeavyRain => "heavy_rain",
        NoiseLevel.BotnetThunderstorm => "botnet_thunderstorm",
        NoiseLevel.BotnetHurricane => "botnet_hurricane",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
    };

    public static string ToDisplayValue(this NoiseLevel level) => level switch
    {
        NoiseLevel.Quiet => "Quiet",
        NoiseLevel.Drizzle => "Drizzle",
        NoiseLevel.Rain => "Rain",
        NoiseLevel.HeavyRain => "Heavy Rain",
        NoiseLevel.BotnetThunderstorm => "Botnet Thunderstorm",
        NoiseLevel.BotnetHurricane => "Botnet Hurricane!",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
    };
}
