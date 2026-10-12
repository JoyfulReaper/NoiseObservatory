# Noise Observatory

Noise Observatory is a small public-facing view of the background Internet noise collected by [TCPNoise](https://github.com/JoyfulReaper/tcpnoise).

The goal is not to build another security dashboard. It is a lightweight, read-only observatory that turns unsolicited connection attempts into useful aggregate statistics, recent sanitized activity, and a little bit of personality.

It is intended to work cleanly over normal HTTP as well as alternative networks such as DN42 and Yggdrasil without depending on clearnet-only assets.

## Goals

The first useful version should show:

- current/recent noise level
- event counts over a few short time windows
- activity grouped by TCPNoise sensor
- top destination ports
- a bounded list of recent sanitized events
- a playful derived status such as `quiet`, `drizzle`, `rain`, or `thunderstorm`

The page should remain useful with JavaScript disabled and when the visitor has no normal Internet access.

## Non-goals

Noise Observatory is **not** intended to be:

- a replacement for Mission Control
- a SIEM or incident-response console
- a packet-capture browser
- a public source-IP database
- a way to expose raw TCPNoise payloads
- a live map of private infrastructure

## Privacy and safety

Public output must be deliberately sanitized.

By default the application should not expose:

- full source IP addresses
- raw payloads or captured application data
- private/internal addresses
- credentials, tokens, headers, or secrets
- detailed firewall or network topology information

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
       +--> bounded recent-event store
       +--> rolling aggregate counters
       +--> noise-level calculation
       |
       v
ASP.NET Core Razor Pages
       |
       +--> DN42
       +--> Yggdrasil
       +--> clearnet (optional)
```

The public Razor page should never receive the raw TCPNoise transport object. Raw fields are mapped into a deliberately limited public `NoiseEvent` before entering public-facing state.

TCPNoise currently publishes live events on:

```text
tcpnoise.connection
```

The raw event includes transport data such as sensor, listen port, IP version, remote address/port, seen count, banner information, and bounded payload data. Noise Observatory intentionally discards fields it does not need for public display.

The MVP intentionally starts without a database. Historical storage can be added later only if it has a clear use.

## Technology

Current stack:

- .NET 10
- ASP.NET Core Razor Pages
- NATS for direct TCPNoise event consumption
- bounded in-memory state for recent events and aggregates
- local/self-hosted CSS and other assets
- no frontend framework

## Development status

The fake-data Razor page and retro observatory UI are working. The next milestone is the raw-event-to-public-event privacy boundary, followed by live NATS ingestion.

See [PLAN.md](PLAN.md) for the implementation sequence.

## License

MIT. See [LICENSE](LICENSE).
