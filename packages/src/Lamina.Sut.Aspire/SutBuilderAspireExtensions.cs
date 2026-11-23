using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Lamina.Sut.Aspire;

/// <summary>
/// Registers the <c>aspire</c> SUT provider, which starts an Aspire AppHost for the run.
/// </summary>
public static class SutBuilderAspireExtensions
{
    private const string ProviderName = "aspire";
    private const string ReadyTimeoutKey = "Lamina:Sut:ReadyTimeout";
    private const string TimeoutMultiplierKey = "Lamina:TimeoutMultiplier";
    private static readonly TimeSpan DefaultReadyTimeout = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Starts <typeparamref name="TAppHost"/> through <c>DistributedApplicationTestingBuilder</c>, passing <paramref name="args"/> straight through.
    /// An endpoint name is an Aspire resource name, resolved to that resource's <c>http</c> endpoint.
    /// The run is ready once every resource is healthy, within <c>Lamina:Sut:ReadyTimeout</c> (a TimeSpan, default 00:02:00)
    /// scaled by <c>Lamina:TimeoutMultiplier</c>.
    /// </summary>
    [SuppressMessage("Major Code Smell", "S4018:Generic methods should provide type parameters", Justification = "Spec-mandated Aspire<TAppHost>(args) signature; the AppHost type can't be inferred.")]
    public static SutBuilder Aspire<TAppHost>(this SutBuilder sut, params string[] args)
        where TAppHost : class =>
        sut.AddProvider(ProviderName, _ => new AspireSutProvider<TAppHost>(args, ReadyTimeout(sut.Configuration)));

    private static TimeSpan ReadyTimeout(IConfiguration configuration)
    {
        var timeout = configuration[ReadyTimeoutKey] is { } configured
            ? TimeSpan.Parse(configured, CultureInfo.InvariantCulture)
            : DefaultReadyTimeout;
        var multiplier = configuration[TimeoutMultiplierKey] is { } scale
            ? double.Parse(scale, CultureInfo.InvariantCulture)
            : 1;
        return timeout * multiplier;
    }
}
