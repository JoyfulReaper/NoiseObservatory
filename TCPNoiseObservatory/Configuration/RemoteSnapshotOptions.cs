namespace TCPNoiseObservatory.Configuration;

public sealed class RemoteSnapshotOptions
{
    public string? Url { get; set; }
    public int PollIntervalSeconds { get; set; } = 10;
}
