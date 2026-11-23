using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Lamina;

/// <summary>
/// Where SUT packages register their named providers. <c>Lamina:Sut:Provider</c> chooses one.
/// </summary>
public sealed class SutBuilder
{
    private const string ProviderKey = "Lamina:Sut:Provider";
    private const string ReadyTimeoutKey = "Lamina:Sut:ReadyTimeout";

    private readonly List<KeyValuePair<string, Func<IServiceProvider, ISutProvider>>> _providers = [];

    internal SutBuilder(IConfiguration configuration) => Configuration = configuration;

    public IConfiguration Configuration { get; }

    /// <summary>
    /// Returns how long a provider waits for the SUT to be ready: <c>Lamina:Sut:ReadyTimeout</c> (seconds, or a TimeSpan such as
    /// <c>00:02:00</c>), else <paramref name="providerDefault"/>, scaled by <c>Lamina:TimeoutMultiplier</c>.
    /// </summary>
    public TimeSpan ReadyTimeout(TimeSpan providerDefault)
    {
        var timeout = Configuration[ReadyTimeoutKey] is { } configured ? ParseTimeout(configured) : providerDefault;
        var multiplier = TimeoutMultiplier.Read(Configuration);
        return timeout * multiplier;
    }

    public SutBuilder AddProvider(string name, Func<IServiceProvider, ISutProvider> factory)
    {
        if (_providers.Exists(p => IsNamed(p, name)))
        {
            throw new InvalidOperationException($"A SUT provider named '{name}' is already registered.");
        }

        _providers.Add(new(name, factory));
        return this;
    }

    internal (string Name, ISutProvider Provider) Select(IServiceProvider services)
    {
        if (_providers.Count == 0)
        {
            throw new InvalidOperationException("No SUT provider is registered. Call UseSut(...) in the suite's Configure.");
        }

        var name = Configuration[ProviderKey];
        var chosen = string.IsNullOrEmpty(name) ? _providers[0] : _providers.Find(p => IsNamed(p, name));
        if (chosen.Value is null)
        {
            throw new InvalidOperationException(
                $"{ProviderKey} is '{name}', which is not a registered SUT provider. Registered: {string.Join(", ", _providers.Select(p => p.Key))}.");
        }

        return (chosen.Key, chosen.Value(services));
    }

    private static TimeSpan ParseTimeout(string configured) =>
        double.TryParse(configured, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.Parse(configured, CultureInfo.InvariantCulture);

    private static bool IsNamed(KeyValuePair<string, Func<IServiceProvider, ISutProvider>> provider, string name) =>
        string.Equals(provider.Key, name, StringComparison.OrdinalIgnoreCase);
}
