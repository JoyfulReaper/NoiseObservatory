# Noise Observatory Plan

This plan is intentionally incremental. The project should become useful quickly without turning into a second monitoring platform.

## MVP definition

A successful first release displays a small public page containing:

- an overall current noise level
- total events for recent windows such as 5 minutes and 1 hour
- event counts per sensor
- top destination ports
- a bounded recent-event feed
- sanitized source information
- no dependency on external CDNs or clearnet APIs

The MVP does not require persistent history.

## Phase 1 — Page shape with fake data

Status: **complete enough to move on**.

Implemented so far:

- .NET 10 ASP.NET Core app
- Razor Pages UI
- `NoiseEvent` / `NoiseSnapshot` public models
- fake snapshot provider
- local/self-hosted CSS
- retro observatory/status-terminal presentation
- derived current-conditions label
- masked fake source addresses

The current page proves the presentation path without NATS in the loop.

## Phase 2 — Raw event contract and sanitization boundary

Implement the privacy boundary independently from live subscription plumbing.

TCPNoise currently publishes on:

```text
tcpnoise.connection
```

A raw event envelope includes fields such as:

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

Noise Observatory should model the raw transport separately from the public model.

Suggested shape:

```text
Messaging/
  TcpNoiseEventEnvelope.cs

Services/
  NoiseEventSanitizer.cs
```

The sanitizer maps a raw TCPNoise event into the deliberately limited public `NoiseEvent` model.

Requirements:

- use the raw event's `OccurredAt` timestamp
- map approved sensor names only
- map listen port to public destination port
- derive public protocol as TCP
- deterministically mask/coarsen IPv4 source addresses
- deterministically mask/coarsen IPv6 source addresses
- omit or generically label private, loopback, link-local, and other non-public addresses
- do not expose raw payload, banner, remote source port, connection number, or seen count by default
- malformed input must not cause unsafe output

### Phase 2 done when

Tests demonstrate that representative IPv4, IPv6, private-address, malformed, and unexpected inputs produce safe public `NoiseEvent` values or are deliberately omitted.

## Phase 3 — Live TCPNoise integration

Noise Observatory will subscribe **directly to NATS**. There is no intermediate ingestion API in the MVP.

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
      +--> Razor Pages
      +--> aggregate calculations
```

Requirements:

- use a hosted background service/subscriber
- reconnect cleanly after NATS interruptions
- cancellation must stop the subscriber promptly
- malformed events should be logged and skipped rather than crash the service
- event ingestion must not block HTTP rendering
- burst traffic must remain bounded in memory
- only sanitized/public event objects enter the recent-event store
- keep NATS private; the web application should not expose broker access

### Phase 3 done when

Live TCPNoise activity appears on the page and disconnect/reconnect testing does not require restarting the app.

## Phase 4 — Bounded event store, aggregation, and noise weather

Replace fake snapshot generation with a singleton bounded event store.

Suggested interface:

```text
INoiseEventStore
- Add(NoiseEvent event)
- GetSnapshot()
```

Initial useful aggregates:

- 5-minute event count
- 1-hour event count
- per-sensor counts
- top destination ports
- optional protocol/IP-family counts later if useful

Define a simple noise-level calculation. Keep thresholds obvious and configurable rather than pretending the labels are scientific.

Current vocabulary:

```text
Quiet
Drizzle
Rain
Heavy Rain
Botnet Thunderstorm
```

The underlying event-rate thresholds should remain deterministic.

## Phase 5 — Deployment

Package for the same lightweight Linux/Docker environment used by the surrounding projects.

Deployment requirements:

- Dockerfile
- health endpoint
- configuration through environment/appsettings
- NATS server/subject through configuration rather than source constants
- bounded memory behavior
- no required external static assets
- DN42 endpoint
- Yggdrasil endpoint
- clearnet endpoint only if desired
- appropriate reverse-proxy configuration

Verify the site from a client that has DN42/Yggdrasil connectivity but no usable clearnet asset access.

## Phase 6 — Nice-to-haves

Only after the MVP is stable:

- tiny historical charts using bounded aggregate buckets
- plain-text `/status.txt`
- JSON snapshot endpoint containing sanitized data only
- RSS/Atom-ish recent-noise feed if amusing/useful
- sensor-specific pages
- public uptime/last-event indicators
- integration with the existing Internet Noise FM idea

Avoid adding a database, SPA framework, authentication system, geolocation pipeline, or long-term raw-event archive unless a concrete requirement appears.

## Testing priorities

Highest-value automated tests:

1. source-address sanitization
2. private/non-public address handling
3. malformed raw event handling
4. approved sensor-name mapping
5. bounded-buffer behavior
6. noise-level threshold calculation
7. snapshot aggregation

## Open decisions

These do not block the next slice:

- exact IPv4 masking granularity
- exact IPv6 masking granularity
- final public sensor display-name mapping
- whether short aggregate history should eventually survive restarts
- whether clearnet deployment is desirable

## Next coding slice

Do only this before touching NATS:

1. add `Messaging/TcpNoiseEventEnvelope.cs` matching the current TCPNoise event contract
2. add `Services/NoiseEventSanitizer.cs`
3. make the sanitizer map raw events to public `NoiseEvent`
4. implement deterministic IPv4 and IPv6 masking
5. reject or generically label non-public source addresses
6. add focused tests for sanitization

Stop there. Once the raw-to-public boundary is trustworthy, wire the NATS subscriber into it.
