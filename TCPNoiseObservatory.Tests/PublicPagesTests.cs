using System.Net;
using Xunit;

namespace TCPNoiseObservatory.Tests;

public sealed class PublicPagesTests : IClassFixture<SnapshotFactory>
{
    private readonly HttpClient _client;

    public PublicPagesTests(SnapshotFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task AboutPageExplainsTheObservatoryAndLinksToDashboard()
    {
        using var response = await _client.GetAsync("/About");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("What am I looking at?", html);
        Assert.Contains("Source IP addresses are masked", html);
        Assert.Contains("href=\"/\"", html);
        Assert.Contains("href=\"/About\"", html);
    }

    [Fact]
    public async Task DashboardInitiallyDisplaysTheActualRefreshInterval()
    {
        using var response = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("id=\"refresh-status\">30s</span>", html);
        Assert.Contains("const intervalMs = 30_000;", html);
        Assert.Contains("href=\"/About\"", html);
        Assert.DoesNotContain("id=\"refresh-status\">10s</span>", html);
    }
}
