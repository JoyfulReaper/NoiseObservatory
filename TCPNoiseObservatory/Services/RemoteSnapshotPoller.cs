using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using TCPNoiseObservatory.Configuration;
using TCPNoiseObservatory.Models;

namespace TCPNoiseObservatory.Services;

internal sealed class RemoteSnapshotPoller : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RemoteSnapshotCache _cache;
    private readonly RemoteSnapshotOptions _options;
    private readonly ILogger<RemoteSnapshotPoller> _logger;

    public RemoteSnapshotPoller(
        IHttpClientFactory httpClientFactory,
        RemoteSnapshotCache cache,
        IOptions<RemoteSnapshotOptions> options,
        ILogger<RemoteSnapshotPoller> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Url))
        {
            return;
        }

        var pollInterval = TimeSpan.FromSeconds(
            Math.Clamp(_options.PollIntervalSeconds, 1, 300));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("remote-snapshot");

                using var response = await client.GetAsync(
                    _options.Url,
                    HttpCompletionOption.ResponseHeadersRead,
                    stoppingToken);

                response.EnsureSuccessStatusCode();

                var snapshot = await response.Content.ReadFromJsonAsync<NoiseSnapshot>(
                    cancellationToken: stoppingToken);

                if (snapshot is not null)
                {
                    _cache.Update(snapshot);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to refresh remote Noise Observatory snapshot from {Url}.",
                    _options.Url);
            }

            try
            {
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
