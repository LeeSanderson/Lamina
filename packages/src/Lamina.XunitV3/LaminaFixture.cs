using Xunit;

namespace Lamina.XunitV3;

/// <summary>
/// The run, as an xUnit assembly fixture. Register it once per suite assembly with
/// <c>[assembly: AssemblyFixture(typeof(LaminaFixture&lt;MySuite&gt;))]</c>.
/// If the run fails to start, the failure is kept and every spec fails with it. Start-up is never retried.
/// The run's start line and teardown time are xUnit diagnostic messages, shown when <c>diagnosticMessages</c> is on in <c>xunit.runner.json</c>.
/// </summary>
public sealed class LaminaFixture<TSuite> : IAsyncLifetime
    where TSuite : LaminaSuite, new()
{
    private LaminaRun? _run;
    private Exception? _startupFailure;

    public LaminaRun Run => _run ?? throw StartupFailed();

    public async ValueTask InitializeAsync()
    {
        try
        {
            _run = await new TSuite().StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
            TestContext.Current.SendDiagnosticMessage(_run.StartLine);
        }
        catch (Exception error)
        {
            _startupFailure = error;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_run is not null)
        {
            await _run.DisposeAsync().ConfigureAwait(false);
            TestContext.Current.SendDiagnosticMessage(_run.TeardownLine!);
        }
    }

    private InvalidOperationException StartupFailed() =>
        _startupFailure is null
            ? new InvalidOperationException($"The {typeof(TSuite).Name} run isn't started yet.")
            : new InvalidOperationException(
                $"The {typeof(TSuite).Name} run failed to start, so no spec can run. Start-up is not retried. {_startupFailure.Message}",
                _startupFailure);
}
