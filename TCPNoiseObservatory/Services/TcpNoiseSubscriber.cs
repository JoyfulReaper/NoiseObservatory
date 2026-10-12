using System.Text.Json;
using NATS.Client.Core;
using NATS.Net;
using TCPNoiseObservatory.Messaging;

namespace TCPNoiseObservatory.Services;

internal sealed class TcpNoiseSubscriber : BackgroundService
{
    private const string NatsUrl = "nats://10.99.0.1:4222"; // TODO - Configurable
    private const string Subject = "tcpnoise.connection"; // TODO - Configurable

    private readonly INoiseEventStore _store;
    private readonly NoiseEventSanitizer _sanitizer;
    private readonly ILogger<TcpNoiseSubscriber> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TcpNoiseSubscriber(
        INoiseEventStore store,
        NoiseEventSanitizer sanitizer,
        ILogger<TcpNoiseSubscriber> logger)
    {
        _store = store;
        _sanitizer = sanitizer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var options = NatsOpts.Default with
        {
            Url = NatsUrl,
            Name = "noise-observatory",
            RetryOnInitialConnect = true
        };

        await using var nats = new NatsClient(options);

        try
        {
            await foreach (var message in nats.SubscribeAsync<string>(
                Subject,
                cancellationToken: stoppingToken))
            {
                TcpNoiseEventEnvelope? rawEvent;

                try
                {
                    rawEvent = JsonSerializer.Deserialize<TcpNoiseEventEnvelope>(message.Data, JsonOptions);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Ignored malformed TCPNoise event.");

                    continue;
                }

                if (rawEvent is null)
                {
                    continue;
                }

                var noiseEvent = _sanitizer.Sanitize(rawEvent);

                if (noiseEvent is null)
                {
                    continue;
                }

                _store.Add(noiseEvent);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}