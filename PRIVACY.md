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

## Data retention

The MVP retains only bounded in-memory recent events and bounded derived state.

It does not create a permanent public history of source activity. State resets when the application restarts.

If persistent history is added later, retention limits and the privacy impact must be deliberately reviewed first.

## External enrichment

Do not send visitor or source addresses to third-party geolocation, reputation, analytics, or enrichment APIs by default.

Besides privacy concerns, Noise Observatory should remain useful from DN42/Yggdrasil without depending on clearnet services.

## Logging

Application logs should avoid reintroducing data that was intentionally removed from the public model.

Errors may need operational context, but do not casually log payloads, secrets, or complete source information at normal log levels.

## Principle

If a field makes the page only slightly more interesting but materially increases data exposure, leave it out.
