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

- .NET 10 / ASP.NET Core
- server-rendered HTML or minimal endpoints
- no SPA framework
- no database unless a concrete requirement appears
- bounded in-memory recent-event state
- NATS integration via a background service when live data is added
- static assets must be local/self-hosted
- application must remain useful without JavaScript

Keep raw TCPNoise transport/event types separate from public view models.

A raw event should be transformed into a deliberately limited public/sanitized object before it can enter any public-facing buffer or view.

## Privacy and security rules

These are hard requirements, not suggestions.

Do not expose by default:

- full source IP addresses
- raw payloads
- captured credentials/tokens/headers
- private/internal addresses
- internal hostnames that are not explicitly approved for public display
- detailed firewall rules
- hidden network topology
- arbitrary raw TCPNoise fields simply because they are available

Any new field shown publicly must be justified as safe and useful.

Treat all event strings as untrusted input and escape them before HTML output.

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

When NATS is added:

- use the existing TCPNoise event contract rather than inventing a parallel schema unless necessary
- use cancellation correctly
- tolerate broker disconnect/reconnect
- do not make page rendering depend synchronously on NATS availability
- sanitize before adding events to the public recent-event buffer

## Testing

Prioritize tests around behavior that can leak data or grow without bounds:

- IPv4/IPv6 sanitization
- private-address handling
- malformed inputs
- HTML escaping
- bounded recent-event buffer behavior
- aggregation limits
- noise-level thresholds

Do not add tests solely to inflate coverage.

## UI guidance

The intended style can be playful/retro, but the output should remain readable.

Prefer:

- semantic HTML
- simple CSS
- good mobile behavior
- no external fonts/CDNs
- no JavaScript dependency for core content
- small page weight

Avoid large UI frameworks unless the project develops a real need for one.

## Documentation

Keep `README.md` for what the project is and how to run it.

Keep `PLAN.md` for implementation phases and pending architectural decisions.

Keep `PRIVACY.md` synchronized with actual public-data behavior.

When a major phase is completed, update the plan rather than leaving stale intentions that contradict the code.

## Scope control

Before adding any of the following, require a concrete reason:

- persistent raw-event storage
- authentication/accounts
- database
- frontend framework
- external API dependency
- IP geolocation
- long-term source-IP history
- packet payload display
- complex plugin architecture

A feature being technically possible is not enough reason to add it.
