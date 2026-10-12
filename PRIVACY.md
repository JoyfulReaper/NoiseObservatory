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
- top-port summaries
- coarse/sanitized source-network information
- derived noise/weather labels

## Not public by default

The following must not be published unless the project explicitly revisits the policy:

- complete source IPv4 addresses
- complete source IPv6 addresses
- raw packet/application payloads
- captured HTTP headers or request bodies
- usernames, passwords, tokens, cookies, or authentication material
- private/internal IP addresses
- internal-only hostnames
- exact firewall policy or non-public service inventory
- arbitrary raw fields copied directly from TCPNoise events

## Address sanitization

The implementation should use deterministic coarse masking rather than ad-hoc string replacement.

Exact masking granularity is still an implementation decision, but the public value must not allow a visitor to reconstruct the full original address.

Private, loopback, link-local, and other non-public source addresses should normally be omitted or represented only by a generic category rather than exposed verbatim.

## Public sensor names

Sensor identifiers shown publicly should come from an explicit allowlist/mapping.

Do not assume an internal TCPNoise sensor ID, machine hostname, cloud instance name, or NATS subject is safe to display merely because it is present in an event.

## Data retention

The MVP should retain only bounded in-memory recent events and bounded rolling aggregates.

It should not create a permanent public history of source activity.

If persistent history is added later, retention limits and the privacy impact must be deliberately reviewed first.

## External enrichment

Do not send visitor or source addresses to third-party geolocation, reputation, analytics, or enrichment APIs by default.

Besides privacy concerns, Noise Observatory should remain useful from DN42/Yggdrasil without depending on clearnet services.

## Logging

Application logs should avoid reintroducing data that was intentionally removed from the public model.

Errors may need operational context, but do not casually log payloads, secrets, or complete source information at normal log levels.

## Principle

If a field makes the page only slightly more interesting but materially increases data exposure, leave it out.
