using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lamina.XunitV3;

/// <summary>
/// Base of every spec class. Each test gets its own spec scope, created in <see cref="InitializeAsync"/> and disposed in
/// <see cref="DisposeAsync"/>. The channel comes from the class's <c>Channel</c> trait, or is the suite's default.
/// </summary>
public abstract class LaminaSpec<TSuite> : IAsyncLifetime
    where TSuite : LaminaSuite, new()
{
    private const string ChannelTrait = "Channel";

    private SpecScope? _scope;

    /// <summary>
    /// Gets this spec's scope. It exists from <see cref="InitializeAsync"/> on, so a spec's constructor can't use it.
    /// </summary>
    protected SpecScope Scope => _scope ?? throw new InvalidOperationException(
        $"{GetType().Name} used its spec scope before InitializeAsync. The spec scope is created in InitializeAsync, " +
        "because xUnit's GetFixture is async, so a spec can't use DSLs from its constructor. Use them from the test method instead.");

    public async ValueTask InitializeAsync()
    {
        var context = TestContext.Current;
        var fixture = await context.GetFixture<LaminaFixture<TSuite>>().ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"No Lamina run for {typeof(TSuite).Name}. Add [assembly: AssemblyFixture(typeof(LaminaFixture<{typeof(TSuite).Name}>))] to the suite's assembly.");
        var test = context.Test ?? throw new InvalidOperationException("xUnit has no current test.");
        var channel = test.Traits.TryGetValue(ChannelTrait, out var channels) ? channels.Single() : null;
        _scope = fixture.Run.CreateScope(channel, test.TestDisplayName);
    }

    public async ValueTask DisposeAsync()
    {
        if (_scope is not null)
        {
            TestContext.Current.TestOutputHelper?.WriteLine(_scope.Summary);
            await _scope.DisposeAsync().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Returns this spec's instance of <typeparamref name="T"/>. Wrap it in a property on the team's spec base.
    /// </summary>
    [SuppressMessage("Critical Code Smell", "S4018:Generic methods should provide type parameters", Justification = "A service lookup: T is what's asked for, and the caller always states it.")]
    protected T Dsl<T>()
        where T : notnull =>
        Scope.Services.GetRequiredService<T>();
}
