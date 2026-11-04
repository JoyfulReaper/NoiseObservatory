using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCPNoiseObservatory.Models;
using TCPNoiseObservatory.Services;
using Xunit;

namespace TCPNoiseObservatory.Tests;

public sealed class TrafficHistoryPageTests(SnapshotFactory factory) : IClassFixture<SnapshotFactory>
{
    [Fact]
    public async Task OlderSnapshotRendersUnavailableHistoryAndExistingStatistics()
    {
        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/");

        Assert.Contains("History is not available in this snapshot yet.", html);
        Assert.DoesNotContain("class=\"history-minute ", html);
        Assert.Contains("Recent activity", html);
        Assert.Contains("id=\"refresh-status\">30s</span>", html);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemoteModeRendersSixtyAccessibleBarsFromSerializedHistory(bool empty)
    {
        var now = new DateTimeOffset(2026, 11, 3, 12, 0, 30, TimeSpan.Zero);
        var counts = new int[60];
        if (!empty)
        {
            counts[0] = 6000;
            counts[30] = 3000;
            counts[59] = 1500;
        }
        var source = new NoiseSnapshot(1500, 10_500, []) { History = new NoiseHistory(now, counts) };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var cache = new RemoteSnapshotCache();
        cache.Update(JsonSerializer.Deserialize<NoiseSnapshot>(JsonSerializer.Serialize(source, options), options)!);
        using var mirror = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RemoteSnapshot:Url", "https://trusted.example/api/snapshot");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<INoiseSnapshotProvider>();
                services.AddSingleton<INoiseSnapshotProvider>(cache);
            });
        });
        using var client = mirror.CreateClient();
        Assert.Null(mirror.Services.GetService<INoiseEventStore>());
        var html = await client.GetStringAsync("/");

        Assert.Equal(60, Regex.Matches(html, "class=\"history-minute ").Count);
        Assert.Equal(60, Regex.Matches(html, "tabindex=\"0\" aria-label=").Count);
        Assert.Contains("11:01", html);
        Assert.Contains("12:00", html);
        Assert.Contains("is-current", html);
        Assert.Contains("current minute, partial", html);
        Assert.Contains("12:00:30 UTC:", html);
        Assert.Contains("Read minute counts", html);
        Assert.Contains("As of 2026-11-03 12:00:30 UTC.", html);
        Assert.DoesNotContain("History is not available", html);
        Assert.DoesNotContain("NaN", html);
        if (empty)
            Assert.Contains("No events recorded in these minutes.", html);
        else
        {
            Assert.Contains("style=\"height: 100%\"", html);
            Assert.Contains("style=\"height: 50%\"", html);
            Assert.Contains("style=\"height: 25%\"", html);
            Assert.Contains(6000.ToString("N0", CultureInfo.CurrentCulture), html);
        }

        var snapshot = await client.GetFromJsonAsync<NoiseSnapshot>("/api/snapshot");
        Assert.Equal(now, snapshot!.History!.AsOf);
        Assert.Equal(counts, snapshot.History.Counts);
    }
}
