# S4018 and S4261 are suppressed per member on spec-fixed APIs

Two Sonar rules clash with signatures and names the spec (#27) and Plan.md fix:

- **S4018** (generic methods should infer their type parameters) fires on registrations and lookups where the caller names the type on purpose: `AddDriver<TCapability, TDriver>`, `AddStub<TStub>`, `AddHttpClient<TClient>`, `AddDslsFromAssemblyOf<T>`, `Aspire<TAppHost>`, `Dsl<T>`, `ReadJsonAsync<T>`. These follow .NET's own `AddScoped<T>()` and `ReadFromJsonAsync<T>()`; a `Type` parameter or a dummy argument would make them worse.
- **S4261** (Async suffix) fires on the domain verbs specs and stubs read as sentences: DSL methods (`PlaceOrder`, `ConfirmOrderConfirmed`), stub verbs (`WillDecline`, `InstallDefaults`, `Respond`) and `Eventually.Until`. Drivers, which specs never call, keep the suffix.

We keep both rules on and suppress each hit with `[SuppressMessage]` and a justification, at class level only where every member needs it (DSL classes, `StubRule`, `HttpResponseMessageExtensions`). Labels are uniform: `"Critical Code Smell", "S4018:..."` and `"Minor Code Smell", "S4261:..."`. Turning the rules off by folder was rejected: it would also hide accidental hits in the same projects. Test projects (`packages/tests/.editorconfig`) turn S4261 off, because test names describe behaviour.
