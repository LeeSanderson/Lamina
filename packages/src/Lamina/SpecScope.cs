using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Lamina;

/// <summary>
/// Everything that belongs to one spec: its test ID, aliases, remembered state, channel and services.
/// </summary>
public sealed class SpecScope : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, object> _remembered = new(StringComparer.Ordinal);
    private readonly AsyncServiceScope _services;
    private int _disposed;

    internal SpecScope(string testId, string channel, string specName, AsyncServiceScope services)
    {
        TestId = testId;
        Channel = channel;
        SpecName = specName;
        Aliases = new Aliases(testId);
        _services = services;
    }

    public string TestId { get; }

    public string Channel { get; }

    public string SpecName { get; }

    public Aliases Aliases { get; }

    /// <summary>
    /// Gets the spec's own DI scope: scoped services such as DSLs are one instance per spec.
    /// </summary>
    public IServiceProvider Services => _services.ServiceProvider;

    /// <summary>
    /// Gets one line naming the test ID, the channel and the aliases in play, for test output and failures.
    /// </summary>
    public string Summary
    {
        get
        {
            var aliases = Aliases.InPlay;
            var described = aliases.Count == 0 ? "none" : string.Join(", ", aliases.Select(a => $"{a.Key} -> {a.Value}"));
            return $"Test {TestId} on '{Channel}' · aliases: {described}";
        }
    }

    public void Remember<T>(Key<T> key, T value)
        where T : notnull =>
        _remembered[key.Name] = value;

    public T Recall<T>(Key<T> key)
        where T : notnull =>
        _remembered.TryGetValue(key.Name, out var value)
            ? Typed(key, value)
            : throw new InvalidOperationException($"Nothing is remembered as '{key.Name}' in this spec.");

    public T Recall<T>(Key<T> key, T orDefault)
        where T : notnull =>
        _remembered.TryGetValue(key.Name, out var value) ? Typed(key, value) : orDefault;

    public async ValueTask DisposeAsync()
    {
        // The DI scope tracks this object too, so disposing the DI scope calls back in here.
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            await _services.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static T Typed<T>(Key<T> key, object value)
        where T : notnull =>
        value is T typed
            ? typed
            : throw new InvalidOperationException($"'{key.Name}' holds a {value.GetType().Name}, not a {typeof(T).Name}.");
}
