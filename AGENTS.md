# AGENTS.md

Guidance for AI-assisted work in this repository.

## Project purpose

Noise Observatory is a small, public, read-only visualization of sanitized TCPNoise activity. It should remain lightweight, understandable, and safe to expose over clearnet, DN42, and Yggdrasil.

Do not turn it into a general monitoring platform, SIEM, packet-analysis suite, or frontend-heavy dashboard.

## Working style

Prefer small, reviewable changes.

Before implementing a feature:

1. inspect the existing code and current plan
2. identify the smallest useful change
3. preserve the privacy boundary
4. avoid speculative abstraction
5. add tests when behavior is security/privacy-sensitive

If requirements are unclear, favor the simpler implementation and leave a short note rather than inventing a framework.

## Architecture constraints

For the current MVP:

- .NET 10 / ASP.NET Core Razor Pages
- no SPA framework
- no database unless a concrete requirement appears
- bounded in-memory recent-event state
- direct NATS subscription via a hosted background service
- no intermediate ingestion API unless a concrete need appears
- static assets must be local/self-hosted
- core content must remain useful without JavaScript

The live path is:

```text
TCPNoise -> tcpnoise.connection -> NATS -> hosted subscriber -> sanitizer -> public event store -> Razor Pages
```

Keep raw TCPNoise transport/event types separate from public view models.

A raw event must be transformed into a deliberately limited public/sanitized object before it can enter any public-facing buffer, aggregate, API response, or view.

Do not let Razor Pages depend on or receive raw NATS event objects.

## Raw TCPNoise event handling

The current TCPNoise event contract contains more data than Noise Observatory needs, including remote addresses/ports, seen counts, banner information, and bounded payload data.

Noise Observatory should deserialize enough of the current contract to consume events reliably, but only carry explicitly approved fields into the public model.

Use the raw event's `OccurredAt` timestamp rather than substituting page-render or receive time where practical.

The current public model intentionally includes `SeenCount`, `BannerSent`, and sanitized banner text. That is an explicit policy choice, not permission to expose additional raw event fields.

## Privacy and security rules

These are hard requirements, not suggestions.

Do not expose by default:

- full source IP addresses
- raw payloads
- remote/source ports
- connection numbers
- captured credentials/tokens/headers
- private/internal addresses except approved overlay-network cases handled by the sanitizer
- internal hostnames that are not explicitly approved for public display
- detailed firewall rules
- hidden network topology
- arbitrary raw TCPNoise fields simply because they are available

Approved current public fields include masked source addresses, destination port, approved sensor name, timestamp, protocol, `SeenCount`, banner-sent state, and sanitized/length-limited banner text.

Banner text must remain treated as untrusted input: control characters must not pass through unchanged, output must remain bounded, and Razor's normal HTML encoding must not be bypassed.

Any new field shown publicly must be justified as safe and useful and should be reflected in `PRIVACY.md`.

Sensor identifiers shown publicly must come from an explicit allowlist or approved mapping rather than automatically trusting internal sensor names.

Treat all event strings as untrusted input. Razor's normal encoding should remain intact; do not introduce raw HTML output for event data.

Do not add geolocation or external enrichment calls without explicit approval. They add privacy, dependency, and clearnet-connectivity concerns.

## Resource limits

The service may receive bursty traffic indirectly through TCPNoise.

Therefore:

- all queues/buffers must be bounded
- event ingestion must not block HTTP requests
- malformed events should be skipped/logged rather than crash the process
- avoid unbounded cardinality maps keyed by attacker-controlled input
- aggregate data should have explicit size/window limits

The current aggregate counts are derived from the bounded retained event buffer, so longer windows can undercount during sufficiently large bursts. Prefer bounded rolling aggregate counters for a future fix rather than making the recent-event buffer unbounded.

## NATS integration

When changing live data handling:

- subscribe directly to the existing `tcpnoise.connection` subject
- use the existing TCPNoise event contract rather than inventing a parallel publisher schema
- keep broker configuration outside source code
- use cancellation correctly
- tolerate broker disconnect/reconnect
- do not make page rendering depend synchronously on NATS availability
- sanitize before adding events to the public recent-event store
- do not expose NATS itself through the public web application

## Testing

Prioritize tests around behavior that can leak data or grow without bounds:

- IPv4/IPv6 sanitization
- private/non-public address handling
- DN42/Yggdrasil overlay handling
- malformed inputs
- approved sensor-name behavior
- banner sanitization and truncation
- bounded recent-event buffer behavior
- aggregation limits
- noise-level thresholds

Do not add tests solely to inflate coverage.

## UI guidance

The current retro observatory/status-terminal direction is intentional and should be preserved unless there is a good reason to change it.

Prefer:

- semantic HTML
- simple CSS
- good mobile behavior
- no external fonts/CDNs
- no JavaScript dependency for core content
- small page weight

Avoid large UI frameworks unless the project develops a real need for one.

## Deployment

The MVP is deployed over clearnet, DN42, and Yggdrasil. Keep deployment changes compatible with the existing Docker/nginx setup unless there is a concrete reason to change it.

Current public endpoints are documented in `README.md` and `PLAN.md`; keep them synchronized when deployment changes.

The Yggdrasil endpoint currently uses plain HTTP over Yggdrasil transport. Do not assume that implies ordinary Internet-routable HTTP exposure.

## Documentation

Keep `README.md` for what the project is and how it works.

Keep `PLAN.md` for implementation phases and pending architectural decisions.

Keep `PRIVACY.md` synchronized with actual public-data behavior.

When a major phase is completed, update the plan rather than leaving stale intentions that contradict the code.

## Scope control

Before adding any of the following, require a concrete reason:

- persistent raw-event storage
- authentication/accounts
- database
- frontend framework
- intermediate ingestion API
- external API dependency
- IP geolocation
- long-term source-IP history
- raw packet payload display
- complex plugin architecture

A feature being technically possible is not enough reason to add it.
