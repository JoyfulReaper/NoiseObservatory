namespace TCPNoiseObservatory.Configuration;

public sealed class TcpNoiseOptions
{
    public string NatsUrl { get; set; } = "nats://10.99.0.1:4222";
    public string Subject { get; set; } = "tcpnoise.connection";

    public HashSet<string> AllowedSensors { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}