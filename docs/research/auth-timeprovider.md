# ASP.NET Core authentication and `TimeProvider`

Research for [issue #8](https://github.com/LeeSanderson/Lamina/issues/8). Plan.md §6.6 tags this claim `[verify]`:

> ASP.NET Core authentication options accept a `TimeProvider` from .NET 8 onwards.

Target: .NET 10. Sources were read on 2026-11-13 at these revisions:

- ASP.NET Core: `dotnet/aspnetcore` branch `release/10.0`, commit [`5d3f090`](https://github.com/dotnet/aspnetcore/tree/5d3f090fddf40048816db21708a5764279996624).
- Microsoft.IdentityModel: tag [`8.19.2`](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/tree/ee19952f21d5c170a778bdca901b67076a1cd8f8). This is the version ASP.NET Core 10 builds against (`IdentityModelVersion` 8.19.2 in [`eng/Versions.props` L14](https://github.com/dotnet/aspnetcore/blob/5d3f090fddf40048816db21708a5764279996624/eng/Versions.props#L14)). Also checked against `dev` HEAD [`0d96aca`](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/tree/0d96aca5a57ab4ab144f33c3603b53f82d4e1d18).

## Answer

**The claim is true but incomplete.** Every ASP.NET Core authentication handler takes a `TimeProvider`, and it comes from DI by default. Cookie authentication uses it for every expiry check. But **JWT bearer and OpenID Connect token-lifetime validation do not use it.** They hand the token to Microsoft.IdentityModel. Its `TokenValidationParameters` has a `TimeProvider`, but the property is `internal` and is always `TimeProvider.System`. So the `exp`/`nbf` check reads real time, and the per-test offset doesn't reach it.

**Workaround:** set `TokenValidationParameters.LifetimeValidator` to a delegate that reads the app's `TimeProvider` (code below).

Before applying it, decide which clock the **token issuer** uses. A lifetime check is only correct when issuer and validator share a clock. See [Design note](#design-note-issuer-and-validator-must-share-a-clock).

## Findings

### 1. The ASP.NET Core authentication layer takes a `TimeProvider` (since .NET 8)

- `AuthenticationSchemeOptions` has `public TimeProvider? TimeProvider { get; set; }`. Its doc comment says "Used for testing." ([AuthenticationSchemeOptions.cs L91-L94](https://github.com/dotnet/aspnetcore/blob/5d3f090fddf40048816db21708a5764279996624/src/Security/Authentication/Core/src/AuthenticationSchemeOptions.cs#L91-L94)). `JwtBearerOptions`, `OpenIdConnectOptions` and `CookieAuthenticationOptions` all derive from it.
- `AddAuthentication()` registers `TimeProvider.System` with `TryAddSingleton` ([AuthenticationServiceCollectionExtensions.cs L26](https://github.com/dotnet/aspnetcore/blob/5d3f090fddf40048816db21708a5764279996624/src/Security/Authentication/Core/src/AuthenticationServiceCollectionExtensions.cs#L26)). So a `TimeProvider` registered by the app wins.
- Every scheme added through `AuthenticationBuilder` gets a post-configure step, `options.TimeProvider ??= <TimeProvider from DI>` ([AuthenticationBuilder.cs L52](https://github.com/dotnet/aspnetcore/blob/5d3f090fddf40048816db21708a5764279996624/src/Security/Authentication/Core/src/AuthenticationBuilder.cs#L52), [L128-L142](https://github.com/dotnet/aspnetcore/blob/5d3f090fddf40048816db21708a5764279996624/src/Security/Authentication/Core/src/AuthenticationBuilder.cs#L128-L142)).
- `AuthenticationHandler<TOptions>` exposes `protected TimeProvider TimeProvider`. It is set from `Options.TimeProvider ?? TimeProvider.System` on each request ([AuthenticationHandler.cs L80](https://github.com/dotnet/aspnetcore/blob/5d3f090fddf40048816db21708a5764279996624/src/Security/Authentication/Core/src/AuthenticationHandler.cs#L80), [L157](https://github.com/dotnet/aspnetcore/blob/5d3f090fddf40048816db21708a5764279996624/src/Security/Authentication/Core/src/AuthenticationHandler.cs#L157)).
- Microsoft Learn: "[TimeProvider] is now a settable property on the `Options` classes for the authentication and identity components. It can be set directly or by registering a provider in the dependency injection container." The change came in ASP.NET Core 8.0 Preview 5 and made `ISystemClock` obsolete ([Breaking change: ISystemClock is obsolete](https://learn.microsoft.com/en-us/aspnet/core/breaking-changes/8/isystemclock-obsolete?view=aspnetcore-10.0)).

**For Lamina:** if `Lamina.Runtime` registers `LaminaTimeProvider` as the DI `TimeProvider`, every authentication handler picks it up. No per-scheme setup is needed. `LaminaTimeProvider` must read the ambient offset on each `GetUtcNow()` call, because the options instance (and the provider) is shared across requests.

### 2. Cookie authentication: uses `TimeProvider` throughout

`CookieAuthenticationHandler` reads `TimeProvider.GetUtcNow()` for:

- the expiry check on each request ([CookieAuthenticationHandler.cs L181-L184](https://github.com/dotnet/aspnetcore/blob/5d3f090fddf40048816db21708a5764279996624/src/Security/Authentication/Cookies/src/CookieAuthenticationHandler.cs#L181-L184));
- sliding-expiration refresh ([L95](https://github.com/dotnet/aspnetcore/blob/5d3f090fddf40048816db21708a5764279996624/src/Security/Authentication/Cookies/src/CookieAuthenticationHandler.cs#L95), [L125](https://github.com/dotnet/aspnetcore/blob/5d3f090fddf40048816db21708a5764279996624/src/Security/Authentication/Cookies/src/CookieAuthenticationHandler.cs#L125));
- `IssuedUtc` at sign-in ([L319](https://github.com/dotnet/aspnetcore/blob/5d3f090fddf40048816db21708a5764279996624/src/Security/Authentication/Cookies/src/CookieAuthenticationHandler.cs#L319)).

The ticket is issued and checked against the same clock, so a per-test offset works. One caveat: for a persistent cookie, the browser enforces the HTTP `Expires` attribute in real time. If the offset moves the clock backwards by more than the cookie lifetime, the browser drops the cookie at once.

### 3. JWT bearer: lifetime validation ignores the handler's `TimeProvider`

- `JwtBearerHandler` never reads `TimeProvider`; the only time-related member is the obsolete `ISystemClock` constructor. It clones `Options.TokenValidationParameters` and calls `tokenHandler.ValidateTokenAsync(token, tvp)` ([JwtBearerHandler.cs L96-L107](https://github.com/dotnet/aspnetcore/blob/5d3f090fddf40048816db21708a5764279996624/src/Security/Authentication/JwtBearer/src/JwtBearerHandler.cs#L96-L107), [L239-L260](https://github.com/dotnet/aspnetcore/blob/5d3f090fddf40048816db21708a5764279996624/src/Security/Authentication/JwtBearer/src/JwtBearerHandler.cs#L239-L260)). It doesn't copy a `TimeProvider` into the parameters.
- `JsonWebTokenHandler` validates lifetime with `Validators.ValidateLifetime(...)` ([JsonWebTokenHandler.ValidateToken.cs L663](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/ee19952f21d5c170a778bdca901b67076a1cd8f8/src/Microsoft.IdentityModel.JsonWebTokens/JsonWebTokenHandler.ValidateToken.cs#L663)). The legacy `JwtSecurityTokenHandler` goes the same way through its virtual `ValidateLifetime`.

### 4. `TokenValidationParameters`: has a `TimeProvider`, but it is internal

- `internal TimeProvider TimeProvider { get; set; } = TimeProvider.System;` ([TokenValidationParameters.cs L564-L567 @ 8.19.2](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/ee19952f21d5c170a778bdca901b67076a1cd8f8/src/Microsoft.IdentityModel.Tokens/TokenValidationParameters.cs#L564-L567)). It is still `internal` on `dev` HEAD ([L576](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/0d96aca5a57ab4ab144f33c3603b53f82d4e1d18/src/Microsoft.IdentityModel.Tokens/TokenValidationParameters.cs#L576)). It appears in `InternalAPI.Shipped.txt`, not the public API. `InternalsVisibleTo` names only IdentityModel's own assemblies and tests, not ASP.NET Core.
- The default lifetime check reads that property: `DateTime utcNow = validationParameters.TimeProvider.GetUtcNow().UtcDateTime;` ([ValidatorUtilities.cs L39](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/ee19952f21d5c170a778bdca901b67076a1cd8f8/src/Microsoft.IdentityModel.Tokens/ValidatorUtilities.cs#L39)). Signing-certificate lifetime checks do the same ([Validators.cs L438](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/ee19952f21d5c170a778bdca901b67076a1cd8f8/src/Microsoft.IdentityModel.Tokens/Validators.cs#L438)).

**So the answer to "TimeProvider or `DateTime.UtcNow`?" is:** the code goes through a `TimeProvider`, but application code can't set it. In practice it is always `TimeProvider.System`, which is real time, the same as `DateTime.UtcNow`.

- `Validators.ValidateLifetime` calls `LifetimeValidator` first, if one is set. The default check is then skipped completely ([Validators.cs L469-L478](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/ee19952f21d5c170a778bdca901b67076a1cd8f8/src/Microsoft.IdentityModel.Tokens/Validators.cs#L469-L478)). The property docs say: "If set, this delegate will be called ... instead of default processing ... Even if `ValidateLifetime` is false, this delegate will still be called." ([TokenValidationParameters.cs L400-L408](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/ee19952f21d5c170a778bdca901b67076a1cd8f8/src/Microsoft.IdentityModel.Tokens/TokenValidationParameters.cs#L400-L408)). If the delegate returns `false`, validation throws `SecurityTokenInvalidLifetimeException` (IDX10230). If it throws, the exception propagates as-is.

### 5. OpenID Connect: partly on `TimeProvider`, partly not

Uses the handler's `TimeProvider`:

- the nonce cookie expiry ([OpenIdConnectHandler.cs L1158](https://github.com/dotnet/aspnetcore/blob/5d3f090fddf40048816db21708a5764279996624/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectHandler.cs#L1158), [L1189](https://github.com/dotnet/aspnetcore/blob/5d3f090fddf40048816db21708a5764279996624/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectHandler.cs#L1189));
- the stored `expires_at` token when `SaveTokens` is on ([L1138](https://github.com/dotnet/aspnetcore/blob/5d3f090fddf40048816db21708a5764279996624/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectHandler.cs#L1138)).

Does not use it:

- **ID token lifetime.** It goes through `Options.TokenValidationParameters.Clone()` and the IdentityModel handler, as for JWT bearer ([L759-L774](https://github.com/dotnet/aspnetcore/blob/5d3f090fddf40048816db21708a5764279996624/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectHandler.cs#L759-L774)). So it has the same real-time behaviour and the same workaround.
- **Nonce timestamp.** `OpenIdConnectProtocolValidator` creates and checks the nonce timestamp with `DateTime.UtcNow` directly ([OpenIdConnectProtocolValidator.cs L91](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/ee19952f21d5c170a778bdca901b67076a1cd8f8/src/Microsoft.IdentityModel.Protocols.OpenIdConnect/OpenIdConnectProtocolValidator.cs#L91), [L661-L663](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/ee19952f21d5c170a778bdca901b67076a1cd8f8/src/Microsoft.IdentityModel.Protocols.OpenIdConnect/OpenIdConnectProtocolValidator.cs#L661-L663)). This is harmless: both sides use real time, so they agree. Leave it alone.
- **Session lifetime from the token.** With `UseTokenLifetime`, the handler copies the ID token's `ValidFrom`/`ValidTo` (IdP real time) into `AuthenticationProperties.IssuedUtc`/`ExpiresUtc` ([L1420-L1433](https://github.com/dotnet/aspnetcore/blob/5d3f090fddf40048816db21708a5764279996624/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectHandler.cs#L1420-L1433)). The cookie handler then checks these against the offset clock. Under a large forward offset, the session expires immediately. `UseTokenLifetime` defaults to `false`.

### 6. Token creation also reads real time

If a test or a fake IdP mints tokens with `JsonWebTokenHandler.CreateToken` and doesn't set times, `iat`/`nbf`/`exp` default from `DateTime.UtcNow` ([JsonWebTokenHandler.CreateToken.cs L764](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/ee19952f21d5c170a778bdca901b67076a1cd8f8/src/Microsoft.IdentityModel.JsonWebTokens/JsonWebTokenHandler.CreateToken.cs#L764); `JwtSecurityTokenHandler` does the same). To issue on the offset clock, set `SecurityTokenDescriptor.IssuedAt`, `NotBefore` and `Expires` from the `TimeProvider` yourself.

## Workaround: a `LifetimeValidator` on the app's `TimeProvider`

The delegate runs on every validation. Because of that, it has to re-implement the default rules: honour `ValidateLifetime`, `RequireExpirationTime` and `ClockSkew`. It should also throw the standard exceptions, so that JWT bearer's `WWW-Authenticate` error description stays the same ("The token expired at ..." comes from the exception type, [JwtBearerHandler.cs L396](https://github.com/dotnet/aspnetcore/blob/5d3f090fddf40048816db21708a5764279996624/src/Security/Authentication/JwtBearer/src/JwtBearerHandler.cs#L396)).

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

public static class TimeProviderLifetimeValidation
{
    public static LifetimeValidator Create(TimeProvider time) =>
        (notBefore, expires, token, p) =>
        {
            if (!p.ValidateLifetime) return true;

            if (expires is null && p.RequireExpirationTime)
                throw new SecurityTokenNoExpirationException("Token has no expiration.");

            if (notBefore is { } nbf && expires is { } e && nbf > e)
                throw new SecurityTokenInvalidLifetimeException("nbf is after exp.")
                    { NotBefore = nbf, Expires = e };

            var now = time.GetUtcNow().UtcDateTime;

            if (notBefore is { } n && n > now + p.ClockSkew)
                throw new SecurityTokenNotYetValidException($"Token not valid before {n:O}; now {now:O}.")
                    { NotBefore = n };

            if (expires is { } x && x < now - p.ClockSkew)
                throw new SecurityTokenExpiredException($"Token expired at {x:O}; now {now:O}.")
                    { Expires = x };

            return true;
        };
}

// Registration: wire it after AddJwtBearer / AddOpenIdConnect, taking TimeProvider from DI.
services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<TimeProvider>((o, time) =>
        o.TokenValidationParameters.LifetimeValidator = TimeProviderLifetimeValidation.Create(time));

services.AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
    .Configure<TimeProvider>((o, time) =>
        o.TokenValidationParameters.LifetimeValidator = TimeProviderLifetimeValidation.Create(time));
```

Notes:

- `LifetimeValidator` is copied by `TokenValidationParameters.Clone()` ([L70](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/ee19952f21d5c170a778bdca901b67076a1cd8f8/src/Microsoft.IdentityModel.Tokens/TokenValidationParameters.cs#L70)), so it survives the per-request clone in both handlers.
- `Lamina.Runtime` could ship this as one opt-in extension, for example `AddLaminaTokenLifetime(scheme)`. It would replace the lifetime check only. Signature, issuer and audience validation stay as they are.
- Signing-certificate lifetime (`ValidateIssuerSigningKeyLifeTime`) still uses real time. There is no public hook for it except `IssuerSigningKeyValidator`. Under normal offsets this rarely matters.
- An alternative is `JwtBearerEvents.OnTokenValidated` with a manual `exp` check. It is weaker: the default real-time check has already run, so a token that is still valid in offset time but expired in real time is rejected before the event fires.

## Design note: issuer and validator must share a clock

A per-test offset only makes token lifetimes testable when the issuer uses the same offset clock.

| Token issuer | Clock it stamps `iat`/`exp` with | Validate lifetime on... |
|---|---|---|
| Real or containerised IdP (Keycloak, Entra) | Real time | **Real time** (the default). Using the offset clock would reject fresh tokens under a forward offset, or accept stale ones. |
| Test token minter inside Lamina or the SUT, with explicit times from `TimeProvider` | Offset time | **Offset time**, through the workaround above |

So the §6.6 sentence "Token validation must use the app's `TimeProvider`" is right only for the second row. For the demo (Folio), use real time for anything that crosses an external IdP. Use the `LifetimeValidator` workaround only where Lamina mints tokens on the offset clock, for example to test "the session expires after N hours".

## Suggested wording for Plan.md §6.6

> **Token lifetimes.** `[verified]` Since .NET 8, ASP.NET Core authentication handlers take `TimeProvider` from DI (`AuthenticationSchemeOptions.TimeProvider`), and cookie expiry follows it. JWT bearer and OIDC token-lifetime validation don't: Microsoft.IdentityModel's `TokenValidationParameters.TimeProvider` is internal and always real time. Where tokens are minted on the offset clock, `Lamina.Runtime` sets `TokenValidationParameters.LifetimeValidator` to a delegate that reads the app's `TimeProvider`. Tokens from a real IdP keep real-time validation.
