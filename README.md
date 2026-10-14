# Noise Observatory

Noise Observatory is a small public-facing view of the background Internet noise collected by [TCPNoise](https://github.com/JoyfulReaper/tcpnoise).

The goal is not to build another security dashboard. It is a lightweight, read-only observatory that turns unsolicited connection attempts into useful aggregate statistics, recent sanitized activity, and a little bit of personality.

It is designed to work cleanly over the normal Internet as well as alternative networks such as DN42, Yggdrasil, and I2P without depending on third-party frontend assets.

## Live endpoints

Noise Observatory is currently reachable through four networks:

- Clearnet: `https://noise.kgivler.com/`
- DN42: `https://noise.joyfulreaper.dn42/`
- Yggdrasil: `http://noise.ygg.kgivler.com/`
- I2P: `http://noise.joyfulreaper.i2p/`

The Yggdrasil service has the dedicated routed address `301:6610:cf9f:bae9::80`.

The clearnet, DN42, and Yggdrasil endpoints are served by the direct/source instance. The I2P endpoint is a second Observatory instance that polls the source instance's sanitized snapshot API and serves that public state locally inside I2P. I2P clients therefore do not need clearnet access themselves, although the I2P gateway currently uses clearnet HTTPS to refresh its sanitized snapshot.

## Public snapshot API

`GET /api/snapshot` returns the existing sanitized snapshot as camelCase JSON with
`Cache-Control: no-store`. In addition to the existing recent-window counts and
sanitized recent events, the snapshot includes derived aggregate fields intended
for machine consumers:

- `level`: stable condition code such as `quiet`, `drizzle`, `rain`, `heavy_rain`, `botnet_thunderstorm`, or `botnet_hurricane`
- `levelDisplay`: the human-facing condition label used by the page
- `eventsPerMinute`: the current five-minute event count divided by five

These values are derived only from public aggregate counts and do not add raw
TCPNoise data to the public contract.

The kgivler.com Lab reads this endpoint directly. Endpoint-scoped CORS permits
GET only from `https://kgivler.com` and `https://www.kgivler.com`, without
credentials or additional request headers. Other routes have no CORS grant.
Server-to-server snapshot polling, including the I2P remote mirror, is unaffected.

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

The public `GET /api/snapshot` endpoint returns only the already-sanitized `NoiseSnapshot` model used by the page. It does not expose the raw TCPNoise transport object or provide access to NATS.

See [PRIVACY.md](PRIVACY.md) for the working data-exposure rules.

## Architecture

Noise Observatory now supports two operating modes.

### Direct/source mode

With `RemoteSnapshot:Url` unset or blank, the application subscribes directly to the existing private NATS event bus:

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
Noise Observatory direct/source instance
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
       +--> Razor Pages
       |      +--> clearnet
       |      +--> DN42
       |      +--> Yggdrasil
       |
       +--> /api/snapshot (sanitized only)
```

The public Razor page and snapshot API never receive the raw TCPNoise transport object. Raw fields are mapped into a deliberately limited public `NoiseEvent` before entering public-facing state.

TCPNoise publishes live events on:

```text
tcpnoise.connection
```

The raw event includes transport data such as sensor, listen port, IP version, remote address/port, seen count, banner information, and bounded payload data. Noise Observatory intentionally discards fields it does not need for public display.

### Remote snapshot mode

When `RemoteSnapshot:Url` is configured, the application does not register the NATS subscriber. Instead it periodically fetches another Observatory instance's sanitized `NoiseSnapshot`, keeps the last successful snapshot in memory, and serves the normal Razor page and `/api/snapshot` from that cache.

The deployed I2P path is:

```text
Noise Observatory direct/source instance
       |
       v
HTTPS /api/snapshot
       |
       v
Noise Observatory remote snapshot instance on i2p-gw
       |
       v
I2P HTTP server tunnel
       |
       v
noise.joyfulreaper.i2p
```

The I2P instance therefore has no NATS access and never receives the raw TCPNoise event contract.

Both modes intentionally have no database. Recent source state is bounded in memory and resets when the direct/source application restarts. A remote instance starts empty until its first successful poll and keeps its last successfully fetched sanitized snapshot if a later poll fails.

## Configuration

Direct/source mode uses the `TcpNoise` configuration section, including the NATS URL, subject, and public sensor allowlist.

Remote snapshot mode is enabled by setting `RemoteSnapshot:Url` (or the corresponding environment variable `RemoteSnapshot__Url`). `RemoteSnapshot:PollIntervalSeconds` controls the polling interval and defaults to 10 seconds.

Example remote-mode environment variables:

```text
RemoteSnapshot__Url=https://noise.kgivler.com/api/snapshot
RemoteSnapshot__PollIntervalSeconds=10
```

## Technology

Current stack:

- .NET 10
- ASP.NET Core Razor Pages
- NATS for direct TCPNoise event consumption
- `HttpClient` polling for optional remote snapshot mirrors
- bounded in-memory state
- Docker deployment
- nginx reverse proxies where appropriate
- Java I2P HTTP server tunnel for the I2P mirror
- local/self-hosted CSS and other assets
- no frontend framework

## Development status

The MVP is live and consuming real TCPNoise events. The raw-event/public-event privacy boundary, NATS subscriber, bounded event store, weather labels, refresh controls, Docker packaging, health endpoint, sanitized snapshot endpoint, remote snapshot mode, and clearnet/DN42/Yggdrasil/I2P deployments are working.

The noise/weather classification is centralized and the public snapshot exposes both a stable machine-readable condition code and the human display label, plus the recent five-minute rate as events per minute. Hysteresis is not currently applied; condition changes are deterministic from the current five-minute count.

Known follow-up work includes improving aggregate accounting so high event rates cannot undercount longer windows when the bounded recent-event store rolls over, refining public sensor display names, and adding tests around privacy-sensitive behavior.

See [PLAN.md](PLAN.md) for implementation history and possible follow-up work.

## License

MIT. See [LICENSE](LICENSE).
