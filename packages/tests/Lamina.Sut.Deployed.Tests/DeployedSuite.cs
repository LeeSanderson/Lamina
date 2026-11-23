namespace Lamina.Sut.Deployed.Tests;

internal sealed class DeployedSuite(IReadOnlyDictionary<string, string> settings, Action<SutBuilder> useSut) : LaminaSuite
{
    public static Task<LaminaRun> StartAsync(IReadOnlyDictionary<string, string> settings) =>
        StartAsync(settings, sut => sut.Deployed());

    public static Task<LaminaRun> StartAsync(IReadOnlyDictionary<string, string> settings, Action<SutBuilder> useSut) =>
        new DeployedSuite(settings, useSut).StartAsync(TestContext.Current.CancellationToken);

    protected override void Configure(LaminaBuilder builder)
    {
        foreach (var (key, value) in settings)
        {
            builder.Configuration[key] = value;
        }

        builder
            .UseSut(useSut)
            .AddChannel("api", isDefault: true);
    }
}
