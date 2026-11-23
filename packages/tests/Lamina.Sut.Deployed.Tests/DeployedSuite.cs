namespace Lamina.Sut.Deployed.Tests;

internal sealed class DeployedSuite(IReadOnlyDictionary<string, string> settings, Action<SutBuilder> useSut, IReadOnlyList<string> needs) : LaminaSuite
{
    public static Task<LaminaRun> StartAsync(IReadOnlyDictionary<string, string> settings) =>
        StartAsync(settings, sut => sut.Deployed());

    public static Task<LaminaRun> StartAsync(IReadOnlyDictionary<string, string> settings, Action<SutBuilder> useSut) =>
        new DeployedSuite(settings, useSut, needs: []).StartAsync(TestContext.Current.CancellationToken);

    // A suite that uses the endpoints in needs, as its HTTP clients and stubs would declare them.
    public static Task<LaminaRun> StartNeedingAsync(IReadOnlyDictionary<string, string> settings, IReadOnlyList<string> needs) =>
        new DeployedSuite(settings, sut => sut.Deployed(), needs).StartAsync(TestContext.Current.CancellationToken);

    protected override void Configure(LaminaBuilder builder)
    {
        foreach (var (key, value) in settings)
        {
            builder.Configuration[key] = value;
        }

        builder
            .UseSut(useSut)
            .AddChannel("api", isDefault: true);
        foreach (var endpoint in needs)
        {
            builder.RequireEndpoint(endpoint);
        }
    }
}
