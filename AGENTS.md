# AGENTS.md

Guidance for AI-assisted work in this repository.

## Project purpose

Noise Observatory is a small, public, read-only visualization of sanitized TCPNoise activity. It should remain lightweight, understandable, and safe to expose over DN42, Yggdrasil, and optionally the normal web.

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

For the MVP:

- .NET 10 / ASP.NET Core Razor Pages
- no SPA framework
- no database unless a concrete requirement appears
- bounded in-memory recent-event state
- direct NATS subscription via a hosted background service
- no intermediate ingestion API unless a concrete need appears
- static assets must be local/self-hosted
- application must remain useful without JavaScript

The intended live path is:

```text
TCPNoise -> tcpnoise.connection -> NATS -> hosted subscriber -> sanitizer -> public event store -> Razor Pages
```

Keep raw TCPNoise transport/event types separate from public view models.

A raw event must be transformed into a deliberately limited public/sanitized object before it can enter any public-facing buffer, aggregate, API response, or view.

Do not let Razor Pages depend on or receive raw NATS event objects.

## Raw TCPNoise event handling

The current TCPNoise event contract contains more data than Noise Observatory needs, including remote addresses/ports, seen counts, banner information, and bounded payload data.

Noise Observatory should deserialize enough of the current contract to consume events reliably, but discard fields that are not approved for public use.

Use the raw event's `OccurredAt` timestamp rather than substituting page-render or receive time where practical.

## Privacy and security rules

These are hard requirements, not suggestions.

Do not expose by default:

- full source IP addresses
- raw payloads
- banner contents
- remote/source ports
- connection numbers
- seen counts
- captured credentials/tokens/headers
- private/internal addresses
- internal hostnames that are not explicitly approved for public display
- detailed firewall rules
- hidden network topology
- arbitrary raw TCPNoise fields simply because they are available

Any new field shown publicly must be justified as safe and useful.

Sensor identifiers shown publicly should come from an explicit approved mapping rather than automatically trusting internal sensor names.

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

## NATS integration

When wiring live data:

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
- malformed inputs
- approved sensor-name mapping
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
- packet payload display
- complex plugin architecture

A feature being technically possible is not enough reason to add it.
