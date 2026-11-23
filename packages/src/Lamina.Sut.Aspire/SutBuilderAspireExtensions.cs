using System.Diagnostics.CodeAnalysis;

namespace Lamina.Sut.Aspire;

/// <summary>
/// Registers the <c>aspire</c> SUT provider, which starts an Aspire AppHost for the run.
/// </summary>
public static class SutBuilderAspireExtensions
{
    private const string ProviderName = "aspire";
    private static readonly TimeSpan DefaultReadyTimeout = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Starts <typeparamref name="TAppHost"/> through <c>DistributedApplicationTestingBuilder</c>, passing <paramref name="args"/> straight through.
    /// An endpoint name is an Aspire resource name, resolved to that resource's <c>http</c> endpoint.
    /// The run is ready once every resource is healthy, within <c>Lamina:Sut:ReadyTimeout</c> (default 120 s)
    /// scaled by <c>Lamina:TimeoutMultiplier</c>.
    /// </summary>
    [SuppressMessage("Critical Code Smell", "S4018:Generic methods should provide type parameters", Justification = "TAppHost names the AppHost to start, as in DistributedApplicationTestingBuilder.CreateAsync<T>(); the caller always states it.")]
    public static SutBuilder Aspire<TAppHost>(this SutBuilder sut, params string[] args)
        where TAppHost : class =>
        sut.AddProvider(ProviderName, _ => new AspireSutProvider<TAppHost>(args, sut.ReadyTimeout(DefaultReadyTimeout)));
}
