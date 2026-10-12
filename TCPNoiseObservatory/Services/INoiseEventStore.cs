using TCPNoiseObservatory.Models;

namespace TCPNoiseObservatory.Services;

public interface INoiseEventStore
{
    void Add(NoiseEvent noiseEvent);
    NoiseSnapshot GetSnapshot();
}