# Boa Constrictor's current scope (and closer .NET competitors)

Research for [issue #10](https://github.com/LeeSanderson/Lamina/issues/10), verifying the Plan.md §1 "What .NET already has" row on Screenplay implementations. Checked 2026-11-13 against the GitHub repos (via `gh api`), the NuGet API, and project docs sites.

## Short answer

- **Boa Constrictor is maintained, but slowly.** It is a Screenplay core plus Selenium, Playwright, RestSharp and xUnit-logging adapters. Last commit 2026-05-04; last NuGet release `Boa.Constrictor.Selenium` 4.4.0 / `Boa.Constrictor` 4.2.0 on 2026-05-22. The core package `Boa.Constrictor.Screenplay` has not changed since 4.0.0 (June 2023).
- **"Single channel per actor" is not quite right.** An actor can hold several abilities at once (for example `BrowseTheWeb` and `CallRestApi`). However, every interaction is hard-bound to one concrete ability type. Nothing lets one spec run unchanged through different channels.
- **No isolation, aliasing or time story.** It has polling (`Wait.Until`), but it runs on a real `Stopwatch` with integer-second timeouts and no clock seam.
- **Closer competitors exist.** **Tranquire** has a library-level mechanism for running the same action at different levels (UI vs API): `ActionTags` / `DispatchAction`. **CSF.Screenplay 2.x** has a per-test DI-scoped "Performance" plus Cast/Personas. Both are active in 2026. **Precept** (new, .NET 10) bundles Screenplay with per-scenario-isolated Playwright.

## Boa Constrictor

Repo: https://github.com/q2ebanking/boa-constrictor (Q2 Banking; 126 stars, 24 open issues, not archived).

### Maintenance

| Signal | Value | Source |
|---|---|---|
| Last commit on `main` | 2026-05-04 "Merge PR #284 update-workflow-versions" (preceded by Selenium bump, PR #283, 2026-05-01) | `gh api repos/q2ebanking/boa-constrictor/commits` |
| Last push | 2026-05-06 | `gh api repos/q2ebanking/boa-constrictor` |
| GitHub Releases | none published | `gh api .../releases` returns empty |
| NuGet `Boa.Constrictor` | 4.2.0, 2026-05-22 | https://www.nuget.org/packages/Boa.Constrictor |
| NuGet `Boa.Constrictor.Selenium` | 4.4.0, 2026-05-22 | https://www.nuget.org/packages/Boa.Constrictor.Selenium |
| NuGet `Boa.Constrictor.Screenplay` (core) | 4.0.0, 2023-06-03 | https://www.nuget.org/packages/Boa.Constrictor.Screenplay |
| NuGet `Boa.Constrictor.Playwright` | 4.0.0, 2025-05-04 | https://www.nuget.org/packages/Boa.Constrictor.Playwright |
| NuGet `Boa.Constrictor.RestSharp` | 4.1.0, 2025-05-04 | https://www.nuget.org/packages/Boa.Constrictor.RestSharp |
| NuGet `Boa.Constrictor.Xunit` | 4.1.0, 2024-04-16 | https://www.nuget.org/packages/Boa.Constrictor.Xunit |

Recent activity is mostly dependency bumps and CI upkeep; the core abstractions are stable/frozen.

### What it ships

From the source tree (`git/trees/main`):

- **Core (`Boa.Constrictor.Screenplay`)**: `Actor`, `IAbility`, `ITask`/`ITaskAsync`, `IQuestion`/`IQuestionAsync`, `ICondition`. It also includes conditions (`IsEqualTo`, `IsGreaterThan`, `ContainsSubstring`, `Matches`, enumerable conditions), `Wait`/`ValueAfterWaiting`/`SetTimeouts`, answer caching (`CacheAnswers`), `RunTasks`, `Safely`/`SafeActions`, loggers (console, list, tee, no-op) and JSON/byte dumpers.
- **Selenium (`Boa.Constrictor.Selenium`)**: ability `BrowseTheWeb`. About 27 tasks (Click, SendKeys, Navigate, SwitchFrame/Window, UploadFile, WaitAndRefresh, ...) and about 28 questions (Text, Appearance, Existence, CurrentUrl, JavaScript, ...).
- **Playwright (`Boa.Constrictor.Playwright`)**: ability `BrowseTheWebWithPlaywright`, about 15 tasks (Click, Fill, Go, Press, SelectOption, ...) and 5 questions.
- **RestSharp (`Boa.Constrictor.RestSharp`)**: ability `CallRestApi` (plus `IRestSharpAbility`/`AbstractRestSharpAbility`, so you can define several API abilities as distinct types). Interactions: `Rest.Request`, `RestApiCall`, `RestApiDownload`, with request/response dumping.
- **xUnit (`Boa.Constrictor.Xunit`)**: only `MessageSinkLogger`.
- Third-party: `SpecFlow.Actions.BoaConstrictor` and `Reqnroll.SpecFlowCompatibility.Actions.BoaConstrictor` (NuGet).

### Channels per actor

`Actor` stores abilities as `IDictionary<Type, IAbility>`. `Can(ability)` adds by `ability.GetType()`, and `Using<TAbility>()` looks up by type (`Boa.Constrictor.Screenplay/Screenplay/Pattern/Actor.cs`). So:

- One actor can hold **many abilities, at most one instance per concrete type**. A web-plus-API actor is supported.
- Each interaction calls `actor.Using<BrowseTheWeb>()`, `Using<BrowseTheWebWithPlaywright>()` and so on directly. Tasks are bound to a concrete channel at compile time. There is **no abstraction that lets one spec or task run through UI, API, or in-process drivers interchangeably**. You would have to hand-roll that indirection yourself.

### Isolation, aliasing, polling, time

- **Per-test isolation**: none. The library has no test-run scope, no fixture or lifetime model, no data partitioning or unique-ID helpers. Actor and ability lifetime is left to the test framework (`Boa.Constrictor.Xunit` only adds logging).
- **Aliasing** (stable logical names mapped to unique per-test data): none found in source.
- **Polling/waiting**: yes. `Wait.Until(question, condition)` with `.And/.Or`, `ForUpTo(seconds)`, `ForAnAdditional(seconds)`, and an optional `SetTimeouts` ability for actor-wide defaults (default 30 s). Implementation: a `do { ... } while (!satisfied && timer.Elapsed.TotalSeconds < ActualTimeout)` loop over a `System.Diagnostics.Stopwatch` (`Screenplay/Waiting/AbstractWait.cs`). Also `Utilities/Retries.cs` and Selenium `WaitAndRefresh`.
- **Time control**: none. No `TimeProvider`/clock abstraction; waiting is wall-clock only, with integer-second granularity.

## Other .NET Screenplay / acceptance-testing libraries

Found via NuGet search (`screenplay`, `screenplay pattern`, `tranquire`) and each project's repo.

### Tranquire (closest on multi-channel)

- Repo https://github.com/Galad/tranquire (30 stars); last commit 2026-10-23; NuGet `Tranquire` 3.0.5-preview (2026-10-28), last stable 1.0.39. About 438k total downloads.
- Packages: `Tranquire`, `Tranquire.Selenium`, `Tranquire.NUnit`. Inspired by Serenity BDD.
- **Multi-level execution is a library feature.** `IActionTags<TTag>` / `ActionTags.Create(...)` let an actor pick the best implementation of an action by tag, and `DispatchAction<T>` picks a different implementation for Given vs When (`src/Tranquire/ActionTags.cs`, `IActionTags.cs`, `DispatchAction.cs`). The demo's `tests/ToDoList.Specifications/Setup.cs` runs the same specs at `TestLevel.UI` or `TestLevel.Api` (API via ASP.NET `TestServer`), chosen by a `TEST_LEVEL` env var. That is close to Lamina's "one spec, several channels" idea, though the levels are an ordered priority list, not a protocol/driver separation.
- No isolation, aliasing or clock features seen. Reporting includes `IMeasureDuration` (measurement only).

### CSF.Screenplay (closest on per-test scope)

- Repo https://github.com/csf-dev/CSF.Screenplay (14 stars); last commit 2026-11-12 (active, docs for Reqnroll, #375). NuGet stable 1.0.0 (2019); **2.0.0-5.alpha** is the current line. Docs: https://csf-dev.github.io/CSF.Screenplay/
- Integrations: NUnit, Reqnroll, SpecFlow (retired). Extensions: Selenium, Web APIs. JSON run report plus HTML converter (docs "Capabilities" page).
- **Per-test scope**: each test is a *Performance* paired with its own DI `IServiceScope` (`CSF.Screenplay/ScopeAndPerformance.cs`). Actors come from a per-performance *Cast*, and are configured via *Personas* and put on the *Stage* with `stage.Spotlight<T>()`. This gives fresh actors and abilities per test. It is lifetime isolation, not data isolation or aliasing.
- Time: `UseAStopwatch` ability for measuring elapsed time (`CSF.Screenplay.Abstractions/Abilities/UseAStopwatch.cs`), plus Selenium waits (`WaitUntilPredicate`, `WaitForSomeTime`, `UseADefaultWaitTime`). No clock control.
- Same channel-binding model as Boa (abilities by type), so no multi-channel spec story.

### Precept (newest; bundled framework)

- Docs https://autom3tion.github.io/precept-docs/; NuGet `Precept.Screenplay` 0.14.0 (2026-11-11), pre-1.0.
- A whole .NET 10 test framework on Microsoft.Testing.Platform with modules for HTTP (`Precept.Api`), browser (`Precept.Web`, Playwright "isolated per scenario"), gRPC and database, Reqnroll support, and `Precept.Screenplay` on top ("actors, abilities, interactions and questions"). Assertions can await eventual outcomes. The docs say nothing about one spec across several channels, aliasing, or time control.
- Worth watching. It is the only one that pairs Screenplay with per-scenario isolation out of the box, though only for browsers.

### Minor / not competitive

- `Agenix.Screenplay` 0.5.0 (2025-08), part of the Agenix.ATF framework.
- `NScreenplay.Core` 0.2.0 (2026-09, 317 downloads).
- `Screenplay` 1.0.1 (2022, about 1.8k downloads), `Screenplay.Xunit`.
- `Cratis.Screenplay*` is unrelated: an event-modelling language, not the test pattern.

No .NET library found that implements the full four-layer model (spec, DSL, protocol driver, SUT) with aliasing and time control.

## Suggested Plan.md §1 row

| Screenplay implementations (Boa Constrictor; also Tranquire, CSF.Screenplay) | A compositional way to write layers 2–3 | Interactions bind to a concrete ability (Selenium/Playwright/RestSharp), so one spec can't switch channel. Tranquire's action tags come closest (UI vs API levels). CSF.Screenplay gives a per-test DI scope. None offers aliasing or a controllable clock (waits are wall-clock polling). |
