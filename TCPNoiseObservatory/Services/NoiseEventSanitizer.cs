using System.Net;
using System.Net.Sockets;
using TCPNoiseObservatory.Messaging;
using TCPNoiseObservatory.Models;

namespace TCPNoiseObservatory.Services;

internal sealed class NoiseEventSanitizer
{
    public NoiseEvent? Sanitize(TcpNoiseEventEnvelope raw)
    {
        var remoteIp = raw.Payload.RemoteAddress;
        if (IPAddress.TryParse(remoteIp, out IPAddress? parsedIp))
        {
            if (IPAddress.IsLoopback(parsedIp))
            {
                return null;
            }
            
            byte[] bytes = parsedIp.GetAddressBytes();

            if (raw.Payload.IpVersion == 4 && parsedIp.AddressFamily == AddressFamily.InterNetwork)
            {
                remoteIp = $"{bytes[0]}.{bytes[1]}.xxx.xxx";
            }
            else if (raw.Payload.IpVersion == 6 && parsedIp.AddressFamily == AddressFamily.InterNetworkV6)
            {
                ushort seg0 = (ushort)((bytes[0] << 8) | bytes[1]);
                ushort seg1 = (ushort)((bytes[2] << 8) | bytes[3]);
                ushort seg2 = (ushort)((bytes[4] << 8) | bytes[5]);

                remoteIp = $"{seg0:x}:{seg1:x}:{seg2:x}:xxx:xxx:xxx:xxx:xxx";
            }
            else
            {
                return null;
            }
        }
        else
        {
            return null;
        }


        var noiseEvent = new NoiseEvent(
            raw.OccurredAt,
            raw.Payload.Sensor,
            "TCP",
            raw.Payload.ListenPort,
            remoteIp
        );

        return noiseEvent;
    }
}