using System.Diagnostics.CodeAnalysis;

namespace Lamina.Stubs;

/// <summary>
/// Base for a team's typed stub of one external system. Take collaborators by constructor; Lamina attaches the spec's
/// scope after construction. Rules and journal queries must be keyed to the spec, and the spec's rules are deleted when
/// it ends.
/// </summary>
public abstract class LaminaStub : IAsyncDisposable
{
    private readonly Lock _gate = new();
    private readonly List<string> _ruleIds = [];
    private SpecScope? _scope;
    private StubContext? _context;

    /// <summary>
    /// Gets the name of the SUT endpoint this stub stands in for, such as <c>payment-provider</c>.
    /// </summary>
    protected abstract string Resource { get; }

    protected SpecScope Scope => _scope ?? throw new InvalidOperationException(
        $"{GetType().Name} has no spec scope: it is installing run defaults, which belong to no spec.");

    protected string TestId => Scope.TestId;

    /// <summary>
    /// Gets the stub's base address as the test process sees it.
    /// </summary>
    protected Uri Endpoint => Context.Sut.Endpoint(Resource);

    protected HttpClient Http => Context.Http;

    private StubContext Context => _context ?? throw new InvalidOperationException(
        $"{GetType().Name} isn't attached to a run. Resolve stubs from a spec's services, and register them with AddStub<T>().");

    private bool InSpec => _scope is not null;

    public async ValueTask DisposeAsync()
    {
        string[] ids;
        lock (_gate)
        {
            ids = [.. _ruleIds];
            _ruleIds.Clear();
        }

        if (ids.Length > 0)
        {
            await DeleteRulesAsync(ids).ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }

    internal Task InstallDefaultsAtRunStartAsync() => InstallDefaults();

    internal void Attach(SpecScope? scope, StubContext context)
    {
        _scope = scope;
        _context = context;
    }

    /// <summary>
    /// Installs run-level rules that match anyone's requests, under fixed IDs. Called once per run, after the SUT is
    /// ready and before any spec, on an instance with no spec scope.
    /// </summary>
    [SuppressMessage("Minor Code Smell", "S4261:Methods should be named according to their synchronicities", Justification = "Named by the stub abstraction design (Plan.md §6.5).")]
    protected virtual Task InstallDefaults() => Task.CompletedTask;

    /// <summary>
    /// Throws unless <paramref name="query"/> contains this spec's test ID, so a rule or journal query can never match
    /// another spec's requests.
    /// </summary>
    protected void RequireKeyedToSpec(string what, string query)
    {
        if (!query.Contains(TestId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{GetType().Name}: {what} isn't keyed to this spec (test {TestId}), so it could match another spec's requests. " +
                "Match on an aliased value in the request, such as Alias(\"visa\"), which always contains the test ID.");
        }
    }

    /// <summary>
    /// Throws when called inside a spec: a rule that isn't keyed to a spec may only be installed at run start.
    /// </summary>
    protected void RequireRunStart(string what)
    {
        if (InSpec)
        {
            throw new InvalidOperationException(
                $"{GetType().Name}: {what} matches every spec's requests, so it can only be installed from InstallDefaults() at run start. " +
                "Inside a spec, add a rule keyed to an aliased value instead.");
        }
    }

    /// <summary>
    /// Records a rule this spec created, so it is deleted when the spec ends.
    /// </summary>
    protected void TrackRule(string id)
    {
        lock (_gate)
        {
            _ruleIds.Add(id);
        }
    }

    /// <summary>
    /// Deletes the given rules from the stub server in one call. Never called with an empty list.
    /// </summary>
    protected abstract Task DeleteRulesAsync(IReadOnlyList<string> ids);
}
