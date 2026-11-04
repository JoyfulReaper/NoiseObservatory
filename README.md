# Noise Observatory

Noise Observatory is a small public-facing view of the background Internet noise collected by [TCPNoise](https://github.com/JoyfulReaper/tcpnoise).

The goal is not to build another security dashboard. It is a lightweight, read-only observatory that turns unsolicited connection attempts into useful aggregate statistics, recent sanitized activity, and a little bit of personality.

It is designed to work cleanly over the normal Internet as well as alternative networks such as DN42, Yggdrasil, and I2P without depending on third-party frontend assets.

## Live endpoints

Noise Observatory is currently reachable through four networks:

- Clearnet: `https://noise.kgivler.com/`
- DN42: `https://noise.joyfulreaper.dn42/`
- Yggdrasil: `http://noise.ygg.kgivler.com/`
- I2P: `http://noise.joyfulreaper.i2p/`

The Yggdrasil service has the dedicated routed address `301:6610:cf9f:bae9::80`.

The clearnet, DN42, and Yggdrasil endpoints are served by the direct/source instance. The I2P endpoint is a second Observatory instance that polls the source instance's sanitized snapshot API and serves that public state locally inside I2P. I2P clients therefore do not need clearnet access themselves, although the I2P gateway currently uses clearnet HTTPS to refresh its sanitized snapshot.

## Public snapshot API

`GET /api/snapshot` returns the existing sanitized snapshot as camelCase JSON with
`Cache-Control: no-store`. In addition to the existing recent-window counts and
sanitized recent events, the snapshot includes derived aggregate fields intended
for machine consumers:

- `level`: stable condition code such as `quiet`, `drizzle`, `rain`, `heavy_rain`, `botnet_thunderstorm`, or `botnet_hurricane`
- `levelDisplay`: the human-facing condition label used by the page
- `eventsPerMinute`: the current five-minute event count divided by five
- optional `history`: `asOf` (source snapshot time in UTC) and `counts` (exactly 60
  nonnegative event counts, oldest first). The final count is the current UTC
  calendar minute; preceding entries cover the previous 59 minutes. Minute starts
  are derived by rounding `asOf` down to a minute and counting backward.

These values are derived only from public aggregate counts and do not add raw
TCPNoise data to the public contract.

The kgivler.com Lab reads this endpoint directly. Endpoint-scoped CORS permits
GET only from `https://kgivler.com` and `https://www.kgivler.com`, without
credentials or additional request headers. Other routes have no CORS grant.
Server-to-server snapshot polling, including the I2P remote mirror, is unaffected.

## What it shows

The current page includes:

- current derived noise/weather level
- event counts for the last 5 minutes and last hour
- a server-rendered 60-minute traffic history graph
- a bounded recent-event sample
- approved sensor names
- destination ports
- masked source addresses
- TCPNoise `SeenCount`
- whether a banner was sent, with banner text sanitized and length-limited before display
- manual refresh and optional client-side auto-refresh every 30 seconds (the visible countdown uses the same interval)
- an `/About` page explaining the purpose, weather levels, data flow, and privacy protections

The core page remains server-rendered and useful without JavaScript. Static assets are local/self-hosted.

### Event counts and retention

The direct/source instance counts each accepted sanitized event once, regardless of
its `SeenCount`. Five-minute and one-hour totals share a fixed ring of 3,601
one-second buckets, independently of the last 5,000 event arrivals retained for
the recent-event sample. Both totals can exceed 5,000; the page and snapshot still
show at most 100 recent events, sorted by timestamp and then `SeenCount`.

Counting uses event timestamps in UTC. Late/out-of-order arrivals count in the
windows where they still belong. Events older than one hour or later than the
source instance's current time are excluded from counters at ingestion; future
events are not queued for later counting. These checks do not change recent-event
sample behavior. Snapshots expire counts even when no new events arrive.

The oldest partially overlapping second is excluded to avoid counting expired
events. Consequently, events can expire less than one second early; exact window
boundaries are inclusive when the current time is a whole second. The fixed
bucket array uses approximately 56 KiB on a 64-bit runtime, independent of traffic
volume. Additions update one bucket and snapshots scan a fixed number of buckets.
The existing integer snapshot counts saturate at `int.MaxValue` rather than
overflowing. Counters and recent events remain in memory and reset when the
direct/source instance restarts.

### Traffic history graph

The graph shows 60 UTC calendar-minute intervals, oldest on the left and newest
on the right. Completed minutes cover `[minute start, next minute start)`; the
striped final bar covers the current, incomplete minute through the source's
snapshot time. Heights scale to the busiest displayed minute. Hover or keyboard
focus reveals a bar's interval and count, and an expandable minute-count list is
available for touch devices and text reading. Rendering and inspection work
without JavaScript or external chart libraries.

These calendar-minute counts need not sum to the rolling last-hour statistic:
the graph begins at the start of the minute 59 minutes before the current minute,
while the statistic looks back one hour from the snapshot time (with the
sub-second precision limit described above). Each accepted event counts once;
`SeenCount` is not summed. Zero bars mean no events were recorded in the retained
buckets, including periods before startup or while the source was unavailable.

History is calculated during the existing bucket scan when a snapshot is read.
It adds no ingestion work, separate history store, or persistence. Its 60 counts
use the existing integer saturation behavior. History resets with the source's
in-memory counters on restart.

Remote mirrors, including I2P, render the same aggregate history using only
`/api/snapshot`. They preserve its source `asOf` timestamp and retain the last
successful history if polling fails. All existing snapshot fields remain intact;
older consumers can ignore the optional addition. Missing or null history from
an older source displays an unavailable message while statistics and recent
events remain usable. Invalid history lengths or negative counts cause a poll
to fail and leave the last successful snapshot in place.

## Non-goals

Noise Observatory is **not** intended to be:

- a replacement for Mission Control
- a SIEM or incident-response console
- a packet-capture browser
- a public full-source-IP database
- a way to expose raw TCPNoise payloads
- a live map of private infrastructure

## Privacy and safety

Public output is deliberately sanitized.

The application does not expose:

- complete source IP addresses
- raw captured payloads
- private/internal addresses unless they are explicitly approved overlay-network addresses associated with the matching DN42/Yggdrasil sensor
- credentials, tokens, headers, or secrets
- remote/source ports or connection numbers

IPv4 and IPv6 source addresses are deterministically masked before they enter public-facing state. Banner text, when present and marked as sent by TCPNoise, has control characters removed/replaced and is capped before display.

The public `GET /api/snapshot` endpoint returns only the already-sanitized `NoiseSnapshot` model used by the page. It does not expose the raw TCPNoise transport object or provide access to NATS.

See [PRIVACY.md](PRIVACY.md) for the working data-exposure rules.

## Architecture

Noise Observatory now supports two operating modes.

### Direct/source mode

With `RemoteSnapshot:Url` unset or blank, the application subscribes directly to the existing private NATS event bus:

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
Noise Observatory direct/source instance
       |
       v
raw TCPNoise event DTO
       |
       v
sanitizer / public mapping
       |
       v
bounded in-memory public event store
       |
       +--> Razor Pages
       |      +--> clearnet
       |      +--> DN42
       |      +--> Yggdrasil
       |
       +--> /api/snapshot (sanitized only)
```

The public Razor page and snapshot API never receive the raw TCPNoise transport object. Raw fields are mapped into a deliberately limited public `NoiseEvent` before entering public-facing state.

TCPNoise publishes live events on:

```text
tcpnoise.connection
```

The raw event includes transport data such as sensor, listen port, IP version, remote address/port, seen count, banner information, and bounded payload data. Noise Observatory intentionally discards fields it does not need for public display.

### Remote snapshot mode

When `RemoteSnapshot:Url` is configured, the application does not register the NATS subscriber. Instead it periodically fetches another Observatory instance's sanitized `NoiseSnapshot`, keeps the last successful snapshot in memory, and serves the normal Razor page and `/api/snapshot` from that cache.

The deployed I2P path is:

```text
Noise Observatory direct/source instance
       |
       v
HTTPS /api/snapshot
       |
       v
Noise Observatory remote snapshot instance on i2p-gw
       |
       v
I2P HTTP server tunnel
       |
       v
noise.joyfulreaper.i2p
```

The I2P instance therefore has no NATS access and never receives the raw TCPNoise event contract.

Both modes intentionally have no database. Recent source state is bounded in memory and resets when the direct/source application restarts. A remote instance starts empty until its first successful poll and keeps its last successfully fetched sanitized snapshot if a later poll fails.

## Configuration

Direct/source mode uses the `TcpNoise` configuration section, including the NATS URL, subject, and public sensor allowlist.

Remote snapshot mode is enabled by setting `RemoteSnapshot:Url` (or the corresponding environment variable `RemoteSnapshot__Url`). `RemoteSnapshot:PollIntervalSeconds` controls the polling interval and defaults to 10 seconds.

Example remote-mode environment variables:

```text
RemoteSnapshot__Url=https://noise.kgivler.com/api/snapshot
RemoteSnapshot__PollIntervalSeconds=10
```

## Technology

Current stack:

- .NET 10
- ASP.NET Core Razor Pages
- NATS for direct TCPNoise event consumption
- `HttpClient` polling for optional remote snapshot mirrors
- bounded in-memory state
- Docker deployment
- nginx reverse proxies where appropriate
- Java I2P HTTP server tunnel for the I2P mirror
- local/self-hosted CSS and other assets
- no frontend framework

## Development status

The MVP is live and consuming real TCPNoise events. The raw-event/public-event privacy boundary, NATS subscriber, bounded event store, weather labels, refresh controls, Docker packaging, health endpoint, sanitized snapshot endpoint, remote snapshot mode, and clearnet/DN42/Yggdrasil/I2P deployments are working.

The noise/weather classification is centralized and the public snapshot exposes both a stable machine-readable condition code and the human display label, plus the recent five-minute rate as events per minute. Hysteresis is not currently applied; condition changes are deterministic from the current five-minute count.

Rolling aggregate counters now keep five-minute and one-hour counts independent of recent-event sample rollover. Possible follow-up work includes refining public sensor display names and extending tests around privacy-sensitive behavior.

See [PLAN.md](PLAN.md) for implementation history and possible follow-up work.

## License

MIT. See [LICENSE](LICENSE).
