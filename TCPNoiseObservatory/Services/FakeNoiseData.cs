using TCPNoiseObservatory.Models;

namespace TCPNoiseObservatory.Services;

public sealed class FakeNoiseData
{
    public NoiseSnapshot GetSnapshot()
    {
        int eventsLastFiveMinutes = Random.Shared.Next(1, 200);
        int eventsLastHour = eventsLastFiveMinutes + Random.Shared.Next(0, 5000);

        int numEvents = Random.Shared.Next(1, 26);
        List<NoiseEvent> noiseEvents = new();
        for (int i = 0; i < numEvents; i++)
        {
            noiseEvents.Add(GetNoiseEvent());
        }

        var noiseSnapshot = new NoiseSnapshot(eventsLastFiveMinutes, eventsLastHour, noiseEvents);

        return noiseSnapshot;
    }

    private NoiseEvent GetNoiseEvent()
    {
        var sensorNum = Random.Shared.Next(1, 4);

        var ports = new[] { 80, 443, 21, 22 };
        var selectedPort = ports[Random.Shared.Next(ports.Length)];

        var octet1 = Random.Shared.Next(1, 255);
        var octet2 = Random.Shared.Next(1, 255);

        var ip = $"{octet1}.{octet2}.xxx.xxx";

        var noiseEvent = new NoiseEvent(
            Timestamp: DateTimeOffset.UtcNow.AddSeconds(-Random.Shared.Next(0, 300)),
            Sensor: $"FakeSensor-{sensorNum}",
            Protocol: "TCP",
            DestinationPort: selectedPort,
            Source: ip);

        return noiseEvent;
    }
}
