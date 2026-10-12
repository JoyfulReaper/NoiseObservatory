using System.Net;
using System.Net.Sockets;
using TCPNoiseObservatory.Messaging;
using TCPNoiseObservatory.Models;

namespace TCPNoiseObservatory.Services;

internal sealed class NoiseEventSanitizer
{
    private const int MaxBannerLength = 200;

    public NoiseEvent? Sanitize(TcpNoiseEventEnvelope raw)
    {
        var remoteIp = raw.Payload.RemoteAddress;
        if (IPAddress.TryParse(remoteIp, out IPAddress? parsedIp))
        {
            if (IPAddress.IsLoopback(parsedIp))
            {
                return null;
            }

            if (!IsAllowedOverlayAddress(raw.Payload.Sensor, parsedIp)
                && IsPrivateOrLocal(parsedIp))
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

        var banner = raw.Payload.BannerSent
            ? SanitizeBanner(raw.Payload.Banner)
            : null;

        var noiseEvent = new NoiseEvent(
            raw.OccurredAt,
            raw.Payload.Sensor,
            "TCP",
            raw.Payload.ListenPort,
            remoteIp,
            raw.Payload.SeenCount,
            raw.Payload.BannerSent,
            banner
        );

        return noiseEvent;
    }

    private static string? SanitizeBanner(string? banner)
    {
        if (string.IsNullOrWhiteSpace(banner))
        {
            return null;
        }

        var sanitized = string.Concat(
            banner.Select(c => char.IsControl(c) ? ' ' : c))
            .Trim();

        if (sanitized.Length == 0)
        {
            return null;
        }

        return sanitized.Length <= MaxBannerLength
            ? sanitized
            : sanitized[..MaxBannerLength] + "…";
    }

    private static bool IsPrivateOrLocal(IPAddress address)
    {
        var bytes = address.GetAddressBytes();

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return
                bytes[0] == 10 ||
                (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168) ||
                (bytes[0] == 169 && bytes[1] == 254);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return
                address.IsIPv6LinkLocal || (bytes[0] & 0xFE) == 0xFC;
        }

        return true;
    }

    private static bool IsAllowedOverlayAddress(
        string sensor,
        IPAddress address)
    {
        if (sensor.EndsWith("-ygg", StringComparison.OrdinalIgnoreCase))
        {
            return IsYggdrasilAddress(address);
        }

        if (sensor.EndsWith("-dn42", StringComparison.OrdinalIgnoreCase))
        {
            return IsDn42Address(address);
        }

        return false;
    }

    private static bool IsYggdrasilAddress(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();

        // Yggdrasil uses 200::/7.
        // First byte is therefore 0x02 or 0x03.
        return (bytes[0] & 0xFE) == 0x02;
    }

    private static bool IsDn42Address(IPAddress address)
    {
        var bytes = address.GetAddressBytes();

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return
                // 172.20.0.0/14
                (bytes[0] == 172 &&
                 bytes[1] >= 20 &&
                 bytes[1] <= 23) ||

                // 172.31.0.0/16
                (bytes[0] == 172 &&
                 bytes[1] == 31) ||

                // 10.100.0.0/14
                (bytes[0] == 10 &&
                 bytes[1] >= 100 &&
                 bytes[1] <= 103) ||

                // 10.127.0.0/16
                (bytes[0] == 10 &&
                 bytes[1] == 127);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // DN42 IPv6 uses ULA space, fd00::/8.
            return bytes[0] == 0xFD;
        }

        return false;
    }
}