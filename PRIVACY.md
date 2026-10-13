# Noise Observatory Privacy and Public Data Rules

Noise Observatory exists to make background Internet noise interesting without turning collected traffic into a public data dump.

The public site should expose only the minimum information needed to understand aggregate activity.

## Public by default

The following are appropriate for public display:

- event timestamps
- approved public sensor names
- transport protocol
- destination port
- aggregate counts
- TCPNoise `SeenCount`
- coarse/sanitized source-network information
- derived noise/weather labels
- whether TCPNoise reports that a banner was sent
- sanitized, length-limited banner text

Banner text is treated as untrusted display data. Control characters are replaced and the public value is capped before it enters the public event model. Razor's normal output encoding must remain enabled.

## Not public by default

The following must not be published unless the project explicitly revisits the policy:

- complete source IPv4 addresses
- complete source IPv6 addresses
- raw packet/application payloads
- remote/source ports
- connection numbers
- captured HTTP headers or request bodies
- usernames, passwords, tokens, cookies, or authentication material
- private/internal IP addresses, except explicitly recognized overlay-network addresses handled through the normal source masking rules
- internal-only hostnames
- exact firewall policy or non-public service inventory
- arbitrary raw fields copied directly from TCPNoise events

## Address sanitization

The implementation uses deterministic coarse masking rather than ad-hoc string replacement.

Current public masking is:

- IPv4: first two octets retained, remainder masked, for example `185.23.xxx.xxx`
- IPv6: first three hextets retained, remainder masked

The public value must not allow a visitor to reconstruct the full original address.

Private, loopback, link-local, and other non-public source addresses are normally omitted. DN42 and Yggdrasil source addresses may be accepted when the sensor identity explicitly marks the matching overlay network; those addresses are still masked before public display.

## Public sensor names

Sensor identifiers shown publicly must come from an explicit allowlist or approved mapping.

Do not assume an internal TCPNoise sensor ID, machine hostname, cloud instance name, or NATS subject is safe to display merely because it is present in an event.

A friendlier display-name mapping may be added later without changing the raw sensor allowlist.

## Banner data

TCPNoise events may contain banner metadata and banner text. Noise Observatory currently allows that text into the public model only when the event reports `BannerSent`.

Before display:

- null/blank banner values become no public text
- control characters are replaced
- surrounding whitespace is trimmed
- output is capped at 200 characters
- Razor HTML-encodes the resulting string normally

This policy does **not** permit publishing arbitrary captured payload data. `Payload`, request bodies, headers, credentials, or other raw captured application data remain non-public.

## Public snapshot API

`GET /api/snapshot` is a public machine-readable view of the same sanitized state used by the Razor page.

It may contain only the public `NoiseSnapshot` model and its already-sanitized `NoiseEvent` values. Raw TCPNoise transport objects must never be serialized directly by this endpoint.

The snapshot API therefore must not expose:

- raw payload fields
- complete source addresses
- remote/source ports
- connection numbers
- fields rejected by the sanitizer
- access to NATS or private broker configuration

The response is marked `Cache-Control: no-store` so intermediary/browser caching is not intentionally encouraged for this changing telemetry view.

The current I2P deployment uses this endpoint as its upstream data source. The I2P instance runs in remote snapshot mode and receives only this already-sanitized representation; it does not connect to the private NATS broker and does not receive the raw TCPNoise event envelope.

## Remote snapshot mirrors

A remote snapshot instance is a public-state mirror, not a second sanitization boundary.

Only a trusted Observatory snapshot endpoint that already follows these public-data rules should be configured as a mirror upstream. A mirror must not be pointed at a raw telemetry endpoint or used as a reason to weaken the direct/source sanitizer.

The mirror keeps only the last successfully fetched `NoiseSnapshot` in memory. If polling fails, it may continue serving that last sanitized snapshot until a later refresh succeeds.

## Data retention

The direct/source MVP retains only bounded in-memory recent events and bounded derived state.

It does not create a permanent public history of source activity. Source state resets when the direct/source application restarts.

Remote snapshot instances also keep only in-memory public state. They start empty until the first successful poll and then retain only the most recently fetched sanitized snapshot.

If persistent history is added later, retention limits and the privacy impact must be deliberately reviewed first.

## External enrichment and dependencies

Do not send visitor or source addresses to third-party geolocation, reputation, analytics, or enrichment APIs by default.

Besides privacy concerns, Noise Observatory should remain useful to clients on DN42, Yggdrasil, and I2P without requiring those clients to access clearnet services or third-party frontend resources.

The current I2P gateway does make an outbound HTTPS request to the clearnet Observatory's sanitized `/api/snapshot` endpoint. That is an intentional server-side deployment dependency; it does not send I2P visitor information to the snapshot source and does not grant the gateway access to raw NATS data.

## Logging

Application logs should avoid reintroducing data that was intentionally removed from the public model.

Errors may need operational context, but do not casually log payloads, secrets, or complete source information at normal log levels.

Remote snapshot logging should record operational failures without dumping full response bodies or introducing raw data into logs.

## Principle

If a field makes the page only slightly more interesting but materially increases data exposure, leave it out.
