using TCPNoiseObservatory.Models;

namespace TCPNoiseObservatory.Services;

public interface INoiseSnapshotProvider
{
    NoiseSnapshot GetSnapshot();
}
