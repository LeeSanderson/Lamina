namespace Lamina.Sut.Deployed;

/// <summary>
/// Registers the <c>deployed</c> SUT provider, which points the run at a system that's already running.
/// </summary>
public static class SutBuilderDeployedExtensions
{
    private const string ProviderName = "deployed";
    private static readonly TimeSpan DefaultReadyTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Adds <c>deployed</c> alongside another provider, such as <c>Aspire&lt;TAppHost&gt;(args)</c>.
    /// Choose it with <c>Lamina:Sut:Provider=deployed</c>. See <see cref="Deployed"/> for its configuration.
    /// </summary>
    public static SutBuilder OrDeployed(this SutBuilder sut) => sut.Deployed();

    /// <summary>
    /// Adds <c>deployed</c>, which reads <c>Lamina:Sut:Deployed:Endpoints:&lt;name&gt;</c> (URLs),
    /// <c>Readiness:&lt;name&gt;</c> (a path polled until it answers 2xx; a name without one isn't checked) and <c>Metadata:*</c>.
    /// Readiness waits up to <c>Lamina:Sut:ReadyTimeout</c> (default 60 s) scaled by <c>Lamina:TimeoutMultiplier</c>.
    /// Teardown leaves the system running.
    /// </summary>
    public static SutBuilder Deployed(this SutBuilder sut) =>
        sut.AddProvider(ProviderName, _ => new DeployedSutProvider(sut.Configuration, sut.ReadyTimeout(DefaultReadyTimeout)));
}
