# Noise Observatory Plan

This plan is intentionally incremental. The project should remain useful and small rather than turning into a second monitoring platform.

## MVP definition

A successful first release displays a small public page containing:

- an overall current noise level
- total events for recent windows such as 5 minutes and 1 hour
- a bounded recent-event feed
- sanitized source information
- destination ports and approved sensor names
- no dependency on external CDNs or frontend assets
- direct live ingestion from TCPNoise through private NATS for the source instance

The MVP does not require persistent history.

## Current status

Status: **MVP deployed**.

The Observatory is currently live over:

- clearnet: `https://noise.kgivler.com/`
- DN42: `https://noise.joyfulreaper.dn42/`
- Yggdrasil: `http://noise.ygg.kgivler.com/`
- I2P: `http://noise.joyfulreaper.i2p/`

The Yggdrasil endpoint uses dedicated routed address `301:6610:cf9f:bae9::80`.

Clearnet, DN42, and Yggdrasil are served by the direct/source instance. I2P is served by a second Noise Observatory instance on `i2p-gw` running in remote snapshot mode. That instance polls the sanitized public snapshot from the source deployment and does not connect to NATS.

## Phase 1 — Page shape with fake data

Status: **complete**.

Implemented:

- .NET 10 ASP.NET Core app
- Razor Pages UI
- `NoiseEvent` / `NoiseSnapshot` public models
- local/self-hosted CSS
- retro observatory/status-terminal presentation
- derived current-conditions label
- masked source-address presentation

The fake-data provider served its purpose and the page now uses live state.

## Phase 2 — Raw event contract and sanitization boundary

Status: **complete for the MVP**.

TCPNoise publishes on:

```text
tcpnoise.connection
```

The raw event envelope includes fields such as:

```text
OccurredAt
Payload.Sensor
Payload.ConnectionNumber
Payload.ListenPort
Payload.IpVersion
Payload.RemoteAddress
Payload.RemotePort
Payload.SeenCount
Payload.Banner
Payload.BannerSent
Payload.PayloadLength
Payload.Payload
```

Raw transport models remain separate from the public `NoiseEvent` model.

Current sanitizer behavior includes:

- uses the raw event's `OccurredAt` timestamp
- maps listen port to public destination port
- reports the public protocol as TCP
- deterministically masks IPv4 and IPv6 source addresses
- rejects malformed addresses and IP-version mismatches
- rejects loopback/private/local addresses unless the sensor/address combination is an explicitly recognized DN42 or Yggdrasil overlay case
- carries `SeenCount` into the public model
- carries banner state into the public model
- sanitizes banner text by replacing control characters and limiting it to 200 characters
- never carries raw payload data, remote source port, or connection number into the public model

The subscriber also applies the configured sensor allowlist before events enter public state.

## Phase 3 — Live TCPNoise integration

Status: **complete**.

Direct/source architecture:

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
TcpNoiseSubscriber : BackgroundService
      |
      v
raw event DTO
      |
      v
NoiseEventSanitizer
      |
      v
NoiseEventStore
      |
      v
Razor Pages / sanitized snapshot API
```

Implemented behavior:

- direct NATS subscription through a hosted background service
- configurable NATS URL and subject
- cancellation-aware subscription
- malformed JSON is logged and skipped
- private NATS remains separate from public HTTP access
- sanitization happens before events enter the public event store
- public rendering does not depend synchronously on NATS availability

## Phase 4 — Bounded event store, aggregation, and noise weather

Status: **complete enough for the MVP**.

Current store behavior:

- singleton bounded in-memory store
- maximum retained event count is bounded
- recent public sample is bounded
- 5-minute event count
- 1-hour event count
- deterministic noise/weather classification
- stable machine-readable noise level code in the public snapshot
- human-facing display label derived from the same classification
- recent `eventsPerMinute` derived from the five-minute count

Current thresholds and vocabulary:

```text
0-34    quiet                / Quiet
35-49   drizzle              / Drizzle
50-174  rain                 / Rain
175-324 heavy_rain           / Heavy Rain
325-499 botnet_thunderstorm  / Botnet Thunderstorm
500+    botnet_hurricane     / Botnet Hurricane!
```

The Razor page and `/api/snapshot` use the same centralized classification so machine consumers do not need to parse display text. The public rate is derived as `EventsLastFiveMinutes / 5.0`.

Hysteresis is not currently implemented. If real deployments show condition flapping near thresholds, add it deliberately rather than embedding separate thresholds in the UI or API.

Known limitation: recent-window aggregates are currently calculated from the bounded retained event queue. If traffic exceeds the retained capacity within an aggregate window, the longer-window count can undercount. A future improvement should keep bounded rolling aggregate counters separately from the recent-event sample.

## Phase 5 — Initial deployment

Status: **complete**.

Implemented:

- Dockerfile
- `/health` endpoint
- configuration through appsettings/environment variables
- NATS server/subject outside source constants
- bounded memory behavior
- no required external static assets
- clearnet deployment
- DN42 deployment with TLS
- Yggdrasil deployment
- nginx reverse proxies

The Yggdrasil deployment uses a dedicated address from ScopeCreep's routed Yggdrasil subnet and a small systemd unit to restore that service address before nginx starts.

## Phase 6 — Sanitized snapshot mirroring and I2P

Status: **complete**.

A concrete deployment need arose for a frontend that cannot reach the private NATS network directly. Rather than exposing NATS or extending the private VPN to the I2P gateway, Noise Observatory gained a deliberately narrow public snapshot path.

Implemented:

- `GET /api/snapshot` returning only the already-sanitized public `NoiseSnapshot`
- `Cache-Control: no-store` on the snapshot response
- `INoiseSnapshotProvider` abstraction shared by direct and mirror modes
- optional `RemoteSnapshot:Url` configuration
- `RemoteSnapshotPoller` background service using `HttpClient`
- bounded in-memory `RemoteSnapshotCache`
- remote mode skips NATS subscriber registration entirely
- last successful sanitized snapshot remains available if a later poll fails
- I2P mirror deployed on `i2p-gw`
- local I2P HTTP server tunnel to the mirror
- `noise.joyfulreaper.i2p` registered and linked from the I2P landing page

Current deployed path:

```text
TCPNoise sensors
      |
      v
     NATS
      |
      v
Noise Observatory direct/source instance
      |
      +--> clearnet
      +--> DN42
      +--> Yggdrasil
      |
      +--> HTTPS /api/snapshot (sanitized only)
                    |
                    v
          Noise Observatory remote mirror
                    |
                    v
                   I2P
```

The I2P mirror currently polls `https://noise.kgivler.com/api/snapshot`. I2P clients do not need clearnet access themselves; the gateway performs the upstream HTTPS fetch.

Current network endpoints:

```text
https://noise.kgivler.com/
https://noise.joyfulreaper.dn42/
http://noise.ygg.kgivler.com/
http://noise.joyfulreaper.i2p/
```

## Phase 7 — Nice-to-haves

Only after the deployed service remains stable:

- rolling aggregate counters independent of the recent-event buffer
- hysteresis for weather-level transitions if threshold flapping is observed
- tiny historical charts using bounded aggregate buckets
- plain-text `/status.txt`
- RSS/Atom-ish recent-noise feed if amusing/useful
- sensor-specific pages
- friendlier public sensor display-name mapping
- public uptime/last-event indicators
- integration with the existing Internet Noise FM idea

Avoid adding a database, SPA framework, authentication system, geolocation pipeline, or long-term raw-event archive unless a concrete requirement appears.

## Testing priorities

Highest-value automated tests:

1. source-address sanitization
2. DN42/Yggdrasil overlay-address handling
3. malformed raw event handling
4. sensor allowlist behavior
5. banner sanitization and truncation
6. bounded-buffer behavior
7. noise-level threshold calculation and stable machine/display mappings
8. snapshot aggregation and serialization contract
9. remote snapshot polling/cache behavior
10. confirmation that remote mode does not register the NATS subscriber

## Open decisions / follow-up questions

These do not block the deployed MVP:

- final public sensor display-name mapping
- whether short aggregate history should ever survive restarts
- whether Yggdrasil should eventually use browser-trusted HTTPS in addition to Yggdrasil's encrypted transport
- whether additional sanitized machine-readable formats beyond `/api/snapshot` would ever be useful

## Next useful coding slice

The highest-value follow-up remains separating rolling aggregate counts from the bounded recent-event queue. That preserves accurate 5-minute/hour counts during bursts while keeping public recent-event memory strictly bounded.
