// PROTOTYPE stand-in for `Lamina.XunitV3`. The only place xUnit types meet Lamina types.
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lamina.XunitV3;

/// <summary>
/// The assembly fixture. One per run: builds the suite's container and starts (or locates) the SUT once.
/// Registered by the suite with <c>[assembly: AssemblyFixture(typeof(LaminaFixture&lt;FolioSuite&gt;))]</c>.
/// </summary>
public sealed class LaminaFixture<TSuite> : IAsyncLifetime
    where TSuite : LaminaSuite, new()
{
    private LaminaRun? run;

    public LaminaRun Run => run ?? throw new InvalidOperationException("Suite not started.");

    public async ValueTask InitializeAsync() => run = await new TSuite().StartAsync();

    public async ValueTask DisposeAsync()
    {
        if (run is not null) await run.DisposeAsync();
    }
}

/// <summary>
/// Base for every spec class. xUnit makes a new instance per test. InitializeAsync creates the spec scope
/// (not the constructor: in xunit.v3 4.x <c>TestContext.Current.GetFixture&lt;T&gt;()</c> is async) and
/// DisposeAsync disposes it. The channel comes from the class's <c>Channel</c> trait, which the channel
/// generator emits; with no trait, the spec runs on the default channel.
/// </summary>
public abstract class LaminaSpec<TSuite> : IAsyncLifetime
    where TSuite : LaminaSuite, new()
{
    public const string ChannelTrait = "Channel";

    private SpecScope? scope;

    protected SpecScope Scope => scope ?? throw new InvalidOperationException("The spec scope is created in InitializeAsync; don't use DSLs from a constructor.");

    // Scoped in DI, so every call in one spec returns the same DSL instance.
    protected T Dsl<T>() where T : notnull => Scope.Services.GetRequiredService<T>();

    public async ValueTask InitializeAsync()
    {
        var context = TestContext.Current;
        var fixture = await context.GetFixture<LaminaFixture<TSuite>>()
            ?? throw new InvalidOperationException(
                $"No Lamina suite fixture. Add [assembly: AssemblyFixture(typeof(LaminaFixture<{typeof(TSuite).Name}>))].");
        var test = context.Test ?? throw new InvalidOperationException("No current test.");
        var channel = test.Traits.TryGetValue(ChannelTrait, out var values) ? values.Single() : null;
        scope = fixture.Run.CreateScope(channel, test.TestDisplayName);
    }

    // Diagnostics for EVERY failure, not just Eventually's: by step 9 of xUnit's pipeline TestState holds the provisional
    // result. Throwing here joins the test's exception aggregator, so the footer lands inside the failure message
    // (console, TRX). Cost: MTP reports an *error* rather than an assertion *failure* (see the xUnit research ticket).
    // The footer also goes to the test output on every run, pass or fail, so passing specs still show their test ID.
    public async ValueTask DisposeAsync()
    {
        var context = TestContext.Current;
        var footer = Scope.Describe();
        context.TestOutputHelper?.WriteLine(footer);
        await Scope.DisposeAsync();
        GC.SuppressFinalize(this);
        if (context.TestState?.Result == TestResult.Failed) throw new LaminaDiagnosticsException(footer);
    }
}

/// <summary>Carries the spec scope's footer into a failing spec's message. Never thrown for a passing spec.</summary>
public sealed class LaminaDiagnosticsException(string footer) : Exception(footer);

/// <summary>Input to the channel generator (not built in this prototype; see Generated/).</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ChannelsAttribute(params string[] channels) : Attribute
{
    public IReadOnlyList<string> Channels => channels;
}
