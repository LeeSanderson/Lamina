namespace Lamina.Drivers.Http.Tests;

internal sealed class HttpSuite(Action<LaminaBuilder> configure, IReadOnlyCollection<string> endpoints) : LaminaSuite
{
    public static Task<LaminaRun> StartAsync(Action<LaminaBuilder> configure) => StartAsync(configure, endpoints: []);

    // The fake SUT lists these endpoint names, so the run checks the suite's needed endpoints against them.
    public static Task<LaminaRun> StartAsync(Action<LaminaBuilder> configure, IReadOnlyCollection<string> endpoints) =>
        new HttpSuite(configure, endpoints).StartAsync(TestContext.Current.CancellationToken);

    protected override void Configure(LaminaBuilder builder)
    {
        builder
            .UseSut(sut => sut.AddProvider("fake", _ => new FakeSut { EndpointNames = endpoints }))
            .AddChannel("api", isDefault: true);
        configure(builder);
    }
}
