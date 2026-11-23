namespace Lamina.Tests;

public sealed class ChannelTests
{
    [Fact]
    public async Task ASpecWithNoChannelRunsOnTheDefaultChannel()
    {
        await using var run = await TestSuite.StartAsync(b => b.AddChannel("web"));
        await using var scope = run.CreateScope(channel: null, specName: "spec");

        scope.Channel.ShouldBe("api");
    }

    [Fact]
    public async Task ASpecRunsOnTheChannelItNames()
    {
        await using var run = await TestSuite.StartAsync(b => b.AddChannel("web"));
        await using var scope = run.CreateScope(channel: "web", specName: "spec");

        scope.Channel.ShouldBe("web");
    }

    [Fact]
    public async Task AChannelTheSuiteDoesNotHaveIsRefusedWithTheChannelsItHas()
    {
        await using var run = await TestSuite.StartAsync(b => b.AddChannel("web"));

        Should.Throw<InvalidOperationException>(() => run.CreateScope(channel: "mobile", specName: "spec"))
            .Message.ShouldBe("Channel 'mobile' isn't added to the suite. Added: api, web.");
    }

    [Fact]
    public async Task ASuiteNeedsADefaultChannel()
    {
        var suite = new NoDefaultChannelSuite();

        var error = await Should.ThrowAsync<InvalidOperationException>(() => suite.StartAsync(TestContext.Current.CancellationToken));

        error.Message.ShouldContain("No default channel");
    }

    [Fact]
    public async Task TheFooterNamesTheTestIdTheChannelAndTheAliasesInPlay()
    {
        await using var run = await TestSuite.StartAsync();
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        scope.Aliases.For("visa");
        scope.Aliases.For("Dune");

        scope.Summary.ShouldBe($"Test {scope.TestId} on 'api' · aliases: visa -> visa-{scope.TestId}, Dune -> Dune-{scope.TestId}");
    }

    private sealed class NoDefaultChannelSuite : LaminaSuite
    {
        protected override void Configure(LaminaBuilder builder) =>
            builder.UseSut(sut => sut.AddProvider("fake", _ => new FakeSut([]))).AddChannel("api");
    }
}
