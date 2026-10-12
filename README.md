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

## Initial architecture

```text
TCPNoise sensors
       |
       v
existing TCPNoise event transport (NATS)
       |
       v
Noise Observatory subscriber
       |
       +--> bounded recent-event buffer
       +--> in-memory aggregate counters
       +--> noise-level calculation
       |
       v
small ASP.NET Core web application
       |
       +--> clearnet (optional)
       +--> DN42
       +--> Yggdrasil
```

The MVP intentionally starts without a database. Historical storage can be added later only if it has a clear use.

## Technology

Planned initial stack:

- .NET 10
- ASP.NET Core
- server-rendered HTML / minimal endpoints
- NATS for TCPNoise event consumption
- in-memory bounded state for the MVP
- local/self-hosted CSS and other assets

No frontend framework is required for the first version.

## Development plan

See [PLAN.md](PLAN.md).

The first milestone is intentionally fake-data-first: build the page shape and aggregation model before connecting it to live TCPNoise events. This keeps presentation, privacy rules, and event transport from becoming one debugging problem.

## License

MIT. See [LICENSE](LICENSE).
