namespace Lamina.Stubs.Tests;

public sealed class RunDefaultsTests
{
    [Fact]
    public async Task DefaultsAreInstalledOnceAfterReadinessAndBeforeAnySpec()
    {
        var (suite, run) = await StubSuite.StartAsync();
        await using var _ = run;
        await using (run.CreateScope(channel: null, specName: "first"))
        {
            suite.Events.Add("first spec");
        }

        await using (run.CreateScope(channel: null, specName: "second"))
        {
            suite.Events.Add("second spec");
        }

        suite.Events.ShouldBe(["sut ready", "defaults installed", "first spec", "second spec"]);
    }

    [Fact]
    public async Task ADefaultInsideASpecIsRefusedWithAnExplanation()
    {
        var (_, run) = await StubSuite.StartAsync();
        await using var _ = run;
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var stub = scope.Services.GetRequiredService<RecordingStub>();

        var refused = Should.Throw<InvalidOperationException>(stub.AddDefault);

        refused.Message.ShouldContain("a default");
        refused.Message.ShouldContain("InstallDefaults");
    }

    [Fact]
    public async Task TheStubFindsItsEndpointOnTheSutByResource()
    {
        var (_, run) = await StubSuite.StartAsync();
        await using var _ = run;
        await using var scope = run.CreateScope(channel: null, specName: "spec");

        scope.Services.GetRequiredService<RecordingStub>().WhereItLives.ShouldBe(new Uri("http://payment-provider.test"));
    }
}
