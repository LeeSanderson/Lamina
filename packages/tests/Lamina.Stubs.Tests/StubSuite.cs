namespace Lamina.Stubs.Tests;

internal sealed class StubSuite(IReadOnlyCollection<string> endpoints) : LaminaSuite
{
    public List<string> Events { get; } = [];

    public static Task<(StubSuite Suite, LaminaRun Run)> StartAsync() => StartAsync(endpoints: []);

    // The fake SUT lists these endpoint names, so the run checks the suite's needed endpoints against them.
    public static async Task<(StubSuite Suite, LaminaRun Run)> StartAsync(IReadOnlyCollection<string> endpoints)
    {
        var suite = new StubSuite(endpoints);
        var run = await suite.StartAsync(TestContext.Current.CancellationToken);
        return (suite, run);
    }

    protected override void Configure(LaminaBuilder builder) =>
        builder
            .UseSut(sut => sut.AddProvider("fake", _ => new FakeSut(Events) { EndpointNames = endpoints }))
            .AddChannel("api", isDefault: true)
            .AddStub<RecordingStub>()
            .Services.AddSingleton(Events);
}
