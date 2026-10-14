using System.Text.Json;
using TCPNoiseObservatory.Models;

namespace TCPNoiseObservatory.Tests;

public sealed class NoiseLevelTests
{
    [Theory]
    [InlineData(0, NoiseLevel.Quiet)]
    [InlineData(34, NoiseLevel.Quiet)]
    [InlineData(35, NoiseLevel.Drizzle)]
    [InlineData(49, NoiseLevel.Drizzle)]
    [InlineData(50, NoiseLevel.Rain)]
    [InlineData(174, NoiseLevel.Rain)]
    [InlineData(175, NoiseLevel.HeavyRain)]
    [InlineData(324, NoiseLevel.HeavyRain)]
    [InlineData(325, NoiseLevel.BotnetThunderstorm)]
    [InlineData(499, NoiseLevel.BotnetThunderstorm)]
    [InlineData(500, NoiseLevel.BotnetHurricane)]
    public void FromFiveMinuteEventCount_UsesExpectedThresholds(int count, NoiseLevel expected)
    {
        Assert.Equal(expected, NoiseLevelClassifier.FromFiveMinuteEventCount(count));
    }

    [Theory]
    [InlineData(NoiseLevel.Quiet, "quiet", "Quiet")]
    [InlineData(NoiseLevel.Drizzle, "drizzle", "Drizzle")]
    [InlineData(NoiseLevel.Rain, "rain", "Rain")]
    [InlineData(NoiseLevel.HeavyRain, "heavy_rain", "Heavy Rain")]
    [InlineData(NoiseLevel.BotnetThunderstorm, "botnet_thunderstorm", "Botnet Thunderstorm")]
    [InlineData(NoiseLevel.BotnetHurricane, "botnet_hurricane", "Botnet Hurricane!")]
    public void NoiseLevel_ProvidesStableApiAndDisplayValues(NoiseLevel level, string apiValue, string displayValue)
    {
        Assert.Equal(apiValue, level.ToApiValue());
        Assert.Equal(displayValue, level.ToDisplayValue());
    }

    [Fact]
    public void SnapshotSerialization_ExposesDerivedMachineReadableConditionAndRate()
    {
        var snapshot = new NoiseSnapshot(214, 1_650, Array.Empty<NoiseEvent>());
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("heavy_rain", root.GetProperty("level").GetString());
        Assert.Equal("Heavy Rain", root.GetProperty("levelDisplay").GetString());
        Assert.Equal(42.8, root.GetProperty("eventsPerMinute").GetDouble(), 3);
        Assert.False(root.TryGetProperty("currentNoiseLevel", out _));
    }
}
