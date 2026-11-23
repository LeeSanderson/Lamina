using Microsoft.Extensions.DependencyInjection;

namespace Lamina;

/// <summary>
/// One run of a suite: its root services, its system under test and the test IDs already used.
/// </summary>
public sealed class LaminaRun : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly ChannelSet _channels;
    private readonly TestIdMinter _testIds;

    internal LaminaRun(ServiceProvider services, ISystemUnderTest sut, ChannelSet channels, TestIdMinter testIds, string startLine)
    {
        _services = services;
        _channels = channels;
        _testIds = testIds;
        Sut = sut;
        StartLine = startLine;
    }

    public ISystemUnderTest Sut { get; }

    public string DefaultChannel => _channels.Default;

    /// <summary>
    /// Gets the run's one diagnostic line: the SUT provider, each endpoint and its URL, the metadata and the time to ready.
    /// </summary>
    public string StartLine { get; }

    /// <summary>
    /// Gets how long teardown took, once <see cref="DisposeAsync"/> has finished; until then, <see langword="null"/>.
    /// </summary>
    public string? TeardownLine { get; private set; }

    /// <summary>
    /// Creates a spec's scope on <paramref name="channel"/>, or on the default channel when it is <see langword="null"/>.
    /// </summary>
    public SpecScope CreateScope(string? channel, string specName)
    {
        var resolved = _channels.Resolve(channel);
        var serviceScope = _services.CreateAsyncScope();
        var scope = new SpecScope(_testIds.Next(), resolved, specName, serviceScope);
        serviceScope.ServiceProvider.GetRequiredService<SpecScopeHolder>().Scope = scope;
        return scope;
    }

    /// <summary>
    /// Tears down in reverse order of start: run services, including run-start hooks, then the system under test.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        var started = TimeProvider.System.GetTimestamp();
        await _services.DisposeAsync().ConfigureAwait(false);
        await Sut.DisposeAsync().ConfigureAwait(false);
        TeardownLine = RunDiagnostics.TeardownLine(TimeProvider.System.GetElapsedTime(started));
    }
}
