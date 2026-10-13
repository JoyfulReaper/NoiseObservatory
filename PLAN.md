# Noise Observatory Plan

This plan is intentionally incremental. The project should remain useful and small rather than turning into a second monitoring platform.

## MVP definition

A successful first release displays a small public page containing:

- an overall current noise level
- total events for recent windows such as 5 minutes and 1 hour
- a bounded recent-event feed
- sanitized source information
- destination ports and approved sensor names
- no dependency on external CDNs or clearnet APIs
- direct live ingestion from TCPNoise through private NATS

The MVP does not require persistent history.

## Current status

Status: **MVP deployed**.

The Observatory is currently live over:

- clearnet: `https://noise.kgivler.com/`
- DN42: `https://noise.joyfulreaper.dn42/`
- Yggdrasil: `http://noise.ygg.kgivler.com/`

The Yggdrasil endpoint uses dedicated routed address `301:6610:cf9f:bae9::80`.

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

Architecture:

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
Razor Pages
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
- deterministic noise/weather labels

Current vocabulary:

```text
Quiet
Drizzle
Rain
Heavy Rain
Botnet Thunderstorm
```

Known limitation: recent-window aggregates are currently calculated from the bounded retained event queue. If traffic exceeds the retained capacity within an aggregate window, the longer-window count can undercount. A future improvement should keep bounded rolling aggregate counters separately from the recent-event sample.

## Phase 5 — Deployment

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

Current network endpoints:

```text
https://noise.kgivler.com/
https://noise.joyfulreaper.dn42/
http://noise.ygg.kgivler.com/
```

The Yggdrasil deployment uses a dedicated address from ScopeCreep's routed Yggdrasil subnet and a small systemd unit to restore that service address before nginx starts.

## Phase 6 — Nice-to-haves

Only after the MVP remains stable:

- rolling aggregate counters independent of the recent-event buffer
- tiny historical charts using bounded aggregate buckets
- plain-text `/status.txt`
- JSON snapshot endpoint containing sanitized data only
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
7. noise-level threshold calculation
8. snapshot aggregation

## Open decisions / follow-up questions

These do not block the deployed MVP:

- final public sensor display-name mapping
- whether short aggregate history should ever survive restarts
- whether Yggdrasil should eventually use browser-trusted HTTPS in addition to Yggdrasil's encrypted transport
- whether to expose a sanitized machine-readable status endpoint beyond `/health`

## Next useful coding slice

The highest-value follow-up is to separate rolling aggregate counts from the bounded recent-event queue. That preserves accurate 5-minute/hour counts during bursts while keeping public recent-event memory strictly bounded.
