namespace Lamina.Stubs.Tests;

internal sealed class StubSuite : LaminaSuite
{
    public List<string> Events { get; } = [];

    public static async Task<(StubSuite Suite, LaminaRun Run)> StartAsync()
    {
        var suite = new StubSuite();
        var run = await suite.StartAsync(TestContext.Current.CancellationToken);
        return (suite, run);
    }

    protected override void Configure(LaminaBuilder builder) =>
        builder
            .UseSut(sut => sut.AddProvider("fake", _ => new FakeSut(Events)))
            .AddChannel("api", isDefault: true)
            .AddStub<RecordingStub>()
            .Services.AddSingleton(Events);
}
