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

Build the application without NATS first.

Suggested initial model:

```text
NoiseEvent
- TimestampUtc
- SensorName
- Protocol
- DestinationPort
- SanitizedSource

NoiseSnapshot
- GeneratedAtUtc
- NoiseLevel
- EventsLast5Minutes
- EventsLastHour
- PerSensorCounts
- TopPorts
- RecentEvents
```

Suggested services:

```text
NoiseEventBuffer
NoiseSnapshotBuilder
NoiseLevelCalculator
```

Requirements:

- use a bounded recent-event buffer
- render server-side HTML
- keep CSS local
- make the page readable on desktop and mobile
- make the page useful without JavaScript
- use generated/fake data until the presentation is understandable

### Phase 1 done when

A browser can open the page and see convincing fake observatory data with no external network dependencies.

## Phase 2 — Sanitization rules

Implement sanitization independently from the event transport.

At minimum:

- never display raw payloads
- mask or coarsen IPv4 source addresses
- mask or coarsen IPv6 source addresses
- avoid publishing internal/private source addresses
- normalize sensor names to explicitly approved public names
- reject or escape control characters and untrusted strings before display

Keep the public model separate from the raw TCPNoise event model so unsafe fields do not accidentally flow directly into the UI.

### Phase 2 done when

Tests demonstrate that representative IPv4, IPv6, private-address, malformed, and unexpected inputs produce safe public output.

## Phase 3 — Live TCPNoise integration

Connect to the existing TCPNoise NATS event stream.

Requirements:

- use a hosted background service/subscriber
- reconnect cleanly after NATS interruptions
- cancellation must stop the subscriber promptly
- malformed events should be logged and skipped rather than crash the service
- event ingestion must not block HTTP rendering
- burst traffic must remain bounded in memory
- only sanitized/public event objects enter the recent-event buffer

### Phase 3 done when

Live TCPNoise activity appears on the page and disconnect/reconnect testing does not require restarting the app.

## Phase 4 — Aggregation and noise weather

Add small rolling summaries rather than storing every event forever.

Initial useful aggregates:

- 5-minute event count
- 1-hour event count
- per-sensor counts
- top destination ports
- optional protocol counts

Define a simple noise-level calculation. Keep thresholds obvious and configurable rather than pretending the labels are scientific.

Example vocabulary:

```text
Quiet
Drizzle
Rain
Heavy Rain
Thunderstorm
Botnet Weather
```

The exact names can stay playful; the underlying event-rate thresholds should remain deterministic.

## Phase 5 — Deployment

Package for the same lightweight Linux/Docker environment used by the surrounding projects.

Deployment requirements:

- Dockerfile
- health endpoint
- configuration through environment/appsettings
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
- JSON snapshot endpoint
- RSS/Atom-ish recent-noise feed if amusing/useful
- sensor-specific pages
- public uptime/last-event indicators
- integration with the existing Internet Noise FM idea
- a deliberately retro observatory visual theme

Avoid adding a database, SPA framework, authentication system, geolocation pipeline, or long-term raw-event archive unless a concrete requirement appears.

## Testing priorities

Highest-value automated tests:

1. source-address sanitization
2. bounded-buffer behavior
3. noise-level threshold calculation
4. snapshot aggregation
5. malformed event handling
6. HTML escaping/public-model safety

## Open decisions

These do not block Phase 1:

- exact IPv4/IPv6 masking granularity
- final public sensor display names
- exact NATS subject/schema used by TCPNoise
- whether clearnet deployment is desirable
- whether short aggregate history should eventually survive restarts
- final visual theme

## First coding session

Do only this:

1. scaffold the .NET 10 ASP.NET Core project
2. add `NoiseEvent` and `NoiseSnapshot`
3. create a fake snapshot provider
4. render one observatory page
5. add basic local CSS

Stop there and look at it before wiring in real events.
