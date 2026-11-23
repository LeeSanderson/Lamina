namespace Lamina;

/// <summary>
/// A spec's two-way map between names and the test-unique values used for them.
/// </summary>
public sealed class Aliases
{
    private readonly Lock _gate = new();
    private readonly string _testId;
    private readonly List<KeyValuePair<string, string>> _inPlay = [];
    private readonly Dictionary<string, string> _forward = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _backward = new(StringComparer.Ordinal);
    private int _uniqueCount;

    internal Aliases(string testId) => _testId = testId;

    /// <summary>
    /// Gets each name used with <see cref="For"/> and its alias, in first-use order.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> InPlay
    {
        get
        {
            lock (_gate)
            {
                return [.. _inPlay];
            }
        }
    }

    /// <summary>
    /// Returns <c>name-testid</c>, the same value every time within the spec.
    /// </summary>
    public string For(string name)
    {
        lock (_gate)
        {
            if (_forward.TryGetValue(name, out var existing))
            {
                return existing;
            }

            var alias = $"{name}-{_testId}";
            _forward[name] = alias;
            _backward[alias] = name;
            _inPlay.Add(new(name, alias));
            return alias;
        }
    }

    /// <summary>
    /// Returns the name an alias or unique value was made from, or the value itself if it is neither.
    /// </summary>
    public string Original(string alias)
    {
        lock (_gate)
        {
            return _backward.GetValueOrDefault(alias, alias);
        }
    }

    /// <summary>
    /// Returns a fresh one-off value starting with <paramref name="prefix"/>.
    /// </summary>
    public string Unique(string prefix)
    {
        lock (_gate)
        {
            _uniqueCount++;
            var value = $"{prefix}-{_testId}-{_uniqueCount}";
            _backward[value] = prefix;
            return value;
        }
    }
}
