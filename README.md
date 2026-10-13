# Noise Observatory

Noise Observatory is a small public-facing view of the background Internet noise collected by [TCPNoise](https://github.com/JoyfulReaper/tcpnoise).

The goal is not to build another security dashboard. It is a lightweight, read-only observatory that turns unsolicited connection attempts into useful aggregate statistics, recent sanitized activity, and a little bit of personality.

It is designed to work cleanly over the normal Internet as well as alternative networks such as DN42 and Yggdrasil without depending on clearnet-only assets.

## Live endpoints

The same Observatory is currently available through three networks:

- Clearnet: `https://noise.kgivler.com/`
- DN42: `https://noise.joyfulreaper.dn42/`
- Yggdrasil: `http://noise.ygg.kgivler.com/`

The Yggdrasil service has the dedicated routed address `301:6610:cf9f:bae9::80`.

## What it shows

The current page includes:

- current derived noise/weather level
- event counts for the last 5 minutes and last hour
- a bounded recent-event sample
- approved sensor names
- destination ports
- masked source addresses
- TCPNoise `SeenCount`
- whether a banner was sent, with banner text sanitized and length-limited before display
- manual refresh and optional client-side auto-refresh

The core page remains server-rendered and useful without JavaScript. Static assets are local/self-hosted.

## Non-goals

Noise Observatory is **not** intended to be:

- a replacement for Mission Control
- a SIEM or incident-response console
- a packet-capture browser
- a public full-source-IP database
- a way to expose raw TCPNoise payloads
- a live map of private infrastructure

## Privacy and safety

Public output is deliberately sanitized.

The application does not expose:

- complete source IP addresses
- raw captured payloads
- private/internal addresses unless they are explicitly approved overlay-network addresses associated with the matching DN42/Yggdrasil sensor
- credentials, tokens, headers, or secrets
- remote/source ports or connection numbers

IPv4 and IPv6 source addresses are deterministically masked before they enter public-facing state. Banner text, when present and marked as sent by TCPNoise, has control characters removed/replaced and is capped before display.

See [PRIVACY.md](PRIVACY.md) for the working data-exposure rules.

## Architecture

Noise Observatory consumes TCPNoise events directly from the existing private NATS event bus. There is no intermediate ingestion API in the MVP.

```text
TCPNoise sensors
       |
       v
 tcpnoise.connection
       |
       v
      NATS
       |
       v
Noise Observatory hosted subscriber
       |
       v
raw TCPNoise event DTO
       |
       v
sanitizer / public mapping
       |
       v
bounded in-memory public event store
       |
       v
ASP.NET Core Razor Pages
       |
       +--> clearnet
       +--> DN42
       +--> Yggdrasil
```

The public Razor page never receives the raw TCPNoise transport object. Raw fields are mapped into a deliberately limited public `NoiseEvent` before entering public-facing state.

TCPNoise publishes live events on:

```text
tcpnoise.connection
```

The raw event includes transport data such as sensor, listen port, IP version, remote address/port, seen count, banner information, and bounded payload data. Noise Observatory intentionally discards fields it does not need for public display.

The MVP intentionally has no database. Recent state is bounded in memory and resets when the application restarts.

## Technology

Current stack:

- .NET 10
- ASP.NET Core Razor Pages
- NATS for direct TCPNoise event consumption
- bounded in-memory state
- Docker deployment
- nginx reverse proxies
- local/self-hosted CSS and other assets
- no frontend framework

## Development status

The MVP is live and consuming real TCPNoise events. The raw-event/public-event privacy boundary, NATS subscriber, bounded event store, weather labels, refresh controls, Docker packaging, health endpoint, and clearnet/DN42/Yggdrasil deployments are working.

Known follow-up work includes improving aggregate accounting so high event rates cannot undercount longer windows when the bounded recent-event store rolls over, refining public sensor display names, and adding tests around privacy-sensitive behavior.

See [PLAN.md](PLAN.md) for implementation history and possible follow-up work.

## License

MIT. See [LICENSE](LICENSE).
