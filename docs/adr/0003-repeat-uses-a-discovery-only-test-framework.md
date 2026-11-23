# Lamina:Repeat uses a discovery-only test framework

Spec #27 said `Lamina:Repeat=N` would expand each spec "using distinct theory arguments", and also that "a custom test framework was rejected". That rejection was about the failure footer (making xUnit report it once instead of in an `AggregateException`), which would have meant replacing how tests run. Repeat needs only discovery: specs are plain `[Fact]`s, and xUnit v3 decides how a `[Fact]` is discovered through its attribute's discoverer or through the framework's discoverer, nothing else.

So `LaminaTestFramework<TSuite>` derives from `XunitTestFramework` and overrides only `CreateDiscoverer`. Its `RepeatingDiscoverer` hands on N copies of each plain test case, named `[copy k/N]`, with a unique ID computed as if the copy number were a theory argument. Execution, fixtures and reporting are xUnit's own. A suite opts in with one assembly attribute next to its fixture.

## Considered options

- **Real theory arguments.** Every spec would become a `[Theory]` with an unused copy parameter and a data source. Rejected: it changes every spec's signature for a CI concern.
- **A Lamina fact attribute (`[Spec]`) with its own discoverer.** Avoids the framework, but every spec, and the channel generator's output, must use it instead of `[Fact]`, and a plain `[Fact]` silently never repeats. Rejected for that trap.
