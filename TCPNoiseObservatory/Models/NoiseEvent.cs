namespace TCPNoiseObservatory.Models;

public sealed record NoiseEvent(
    DateTimeOffset Timestamp,
    string Sensor,
    string Protocol,
    int DestinationPort,
    string Source,
    ulong SeenCount,
    bool BannerSent,
    string? Banner);