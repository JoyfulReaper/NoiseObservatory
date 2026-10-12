using System.Text.Json;
using NATS.Client.Core;
using NATS.Net;
using TCPNoiseObservatory.Messaging;
using Microsoft.Extensions.Options;
using TCPNoiseObservatory.Configuration;

namespace TCPNoiseObservatory.Services;

internal sealed class TcpNoiseSubscriber : BackgroundService
{
    private readonly INoiseEventStore _store;
    private readonly NoiseEventSanitizer _sanitizer;
    private readonly ILogger<TcpNoiseSubscriber> _logger;
    private readonly TcpNoiseOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TcpNoiseSubscriber(
        INoiseEventStore store,
        NoiseEventSanitizer sanitizer,
        IOptions<TcpNoiseOptions> options,
        ILogger<TcpNoiseSubscriber> logger)
    {
        _store = store;
        _sanitizer = sanitizer;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var options = NatsOpts.Default with
        {
            Url = _options.NatsUrl,
            Name = "noise-observatory",
            RetryOnInitialConnect = true
        };

        await using var nats = new NatsClient(options);

        try
        {
            await foreach (var message in nats.SubscribeAsync<string>(
                _options.Subject,
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

                if (_options.AllowedSensors.Count > 0 &&
                    !_options.AllowedSensors.Contains(rawEvent.Payload.Sensor))
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