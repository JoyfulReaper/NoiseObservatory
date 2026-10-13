using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TCPNoiseObservatory.Models;
using TCPNoiseObservatory.Services;
using Xunit;

namespace TCPNoiseObservatory.Tests;

public sealed class SnapshotCorsTests : IClassFixture<SnapshotFactory>
{
    private readonly HttpClient _client;

    public SnapshotCorsTests(SnapshotFactory factory) => _client = factory.CreateClient();

    [Theory]
    [InlineData("https://kgivler.com")]
    [InlineData("https://www.kgivler.com")]
    public async Task ApprovedOriginCanReadSanitizedSnapshot(string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/snapshot");
        request.Headers.Add("Origin", origin);
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
        Assert.True(response.Headers.CacheControl?.NoStore);
        await AssertSnapshotAsync(response);
    }

    [Theory]
    [InlineData("https://foreign.example")]
    [InlineData("http://kgivler.com")]
    [InlineData("https://kgivler.com.foreign.example")]
    [InlineData("null")]
    public async Task OtherOriginsReceiveNoCorsGrant(string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/snapshot");
        request.Headers.Add("Origin", origin);
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task OrdinarySnapshotRequestStillWorks()
    {
        using var response = await _client.GetAsync("/api/snapshot");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.True(response.Headers.CacheControl?.NoStore);
        await AssertSnapshotAsync(response);
    }

    [Fact]
    public async Task HealthHasNoCrossOriginGrant()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "https://www.kgivler.com");
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task GetPreflightIsAllowedWithoutCredentialsOrExtraHeaders()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/snapshot");
        request.Headers.Add("Origin", "https://kgivler.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        using var response = await _client.SendAsync(request);
        Assert.Equal("https://kgivler.com", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("GET", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Methods")));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Headers"));
    }

    [Fact]
    public async Task PostPreflightIsNotGranted()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/snapshot");
        request.Headers.Add("Origin", "https://kgivler.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        using var response = await _client.SendAsync(request);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static async Task AssertSnapshotAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, json.RootElement.GetProperty("eventsLastFiveMinutes").GetInt32());
        var noise = Assert.Single(json.RootElement.GetProperty("recentEvents").EnumerateArray());
        Assert.Equal("185.23.xxx.xxx", noise.GetProperty("source").GetString());
        Assert.Equal("public-test-sensor", noise.GetProperty("sensor").GetString());
        Assert.Equal("TCP", noise.GetProperty("protocol").GetString());
        Assert.Equal(23, noise.GetProperty("destinationPort").GetInt32());
        Assert.False(noise.TryGetProperty("payload", out _));
        Assert.False(noise.TryGetProperty("remoteAddress", out _));
    }
}

public sealed class SnapshotFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Test the real HTTP pipeline without starting NATS or remote polling.
            foreach (var descriptor in services.Where(service =>
                service.ServiceType == typeof(IHostedService) &&
                service.ImplementationType?.Name is "TcpNoiseSubscriber" or "RemoteSnapshotPoller").ToArray())
                services.Remove(descriptor);
            services.RemoveAll<INoiseSnapshotProvider>();
            services.AddSingleton<INoiseSnapshotProvider>(new TestSnapshots());
        });
    }

    private sealed class TestSnapshots : INoiseSnapshotProvider
    {
        public NoiseSnapshot GetSnapshot() => new(1, 1, [new NoiseEvent(
            DateTimeOffset.Parse("2026-10-12T20:00:00Z"), "public-test-sensor",
            "TCP", 23, "185.23.xxx.xxx", 1, false, null)]);
    }
}
