namespace Lamina;

// The SUT endpoints a suite uses, checked once the SUT has started so a missing one fails the run, not every spec.
internal sealed class RequiredEndpoints
{
    private readonly List<Func<IServiceProvider, string>> _names = [];

    internal void Add(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _names.Add(_ => name);
    }

    internal void Add(Func<IServiceProvider, string> name)
    {
        ArgumentNullException.ThrowIfNull(name);
        _names.Add(name);
    }

    // A provider that lists no endpoint names can't be checked; a missing endpoint then fails on first use.
    internal void Check(string providerName, ISystemUnderTest system, IServiceProvider services)
    {
        var known = system.EndpointNames;
        if (known.Count == 0)
        {
            return;
        }

        var missing = _names
            .Select(name => name(services))
            .Distinct(StringComparer.Ordinal)
            .Where(name => !known.Contains(name, StringComparer.Ordinal))
            .ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"The suite needs SUT endpoints that SUT provider '{providerName}' doesn't have: {string.Join(", ", missing)}. " +
                $"It has: {string.Join(", ", known)}.");
        }
    }
}
