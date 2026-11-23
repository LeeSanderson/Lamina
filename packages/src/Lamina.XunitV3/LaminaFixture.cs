using Xunit;

namespace Lamina.XunitV3;

/// <summary>
/// The run, as an xUnit assembly fixture. Register it once per suite assembly with
/// <c>[assembly: AssemblyFixture(typeof(LaminaFixture&lt;MySuite&gt;))]</c>.
/// </summary>
public sealed class LaminaFixture<TSuite> : IAsyncLifetime
    where TSuite : LaminaSuite, new()
{
    private LaminaRun? _run;

    public LaminaRun Run => _run ?? throw new InvalidOperationException($"The {typeof(TSuite).Name} run isn't started yet.");

    public async ValueTask InitializeAsync() =>
        _run = await new TSuite().StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

    public async ValueTask DisposeAsync()
    {
        if (_run is not null)
        {
            await _run.DisposeAsync().ConfigureAwait(false);
        }
    }
}
