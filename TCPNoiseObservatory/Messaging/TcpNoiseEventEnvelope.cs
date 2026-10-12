namespace TCPNoiseObservatory.Messaging;

internal sealed record TcpNoiseEventEnvelope(
    DateTimeOffset OccurredAt,
    TcpNoisePayload Payload);

internal sealed record TcpNoisePayload(
    string Sensor,
    ulong ConnectionNumber,
    ushort ListenPort,
    int IpVersion,
    string RemoteAddress,
    ushort RemotePort,
    ulong SeenCount,
    string? Banner,
    bool BannerSent,
    int PayloadLength,
    string Payload);