namespace Lamina.Tests;

public sealed class AliasTests
{
    [Fact]
    public async Task AnAliasIsTheNameAHyphenAndTheTestId()
    {
        await using var run = await TestSuite.StartAsync();
        await using var scope = run.CreateScope(channel: null, specName: "spec");

        scope.Aliases.For("Dune").ShouldBe($"Dune-{scope.TestId}");
    }

    [Fact]
    public async Task TheSameNameGivesTheSameAliasEveryTimeWithinASpec()
    {
        await using var run = await TestSuite.StartAsync();
        await using var scope = run.CreateScope(channel: null, specName: "spec");

        var first = scope.Aliases.For("Dune");

        scope.Aliases.For("Dune").ShouldBe(first);
    }

    [Fact]
    public async Task OriginalMapsAnAliasBackToItsName()
    {
        await using var run = await TestSuite.StartAsync();
        await using var scope = run.CreateScope(channel: null, specName: "spec");

        var alias = scope.Aliases.For("Dune");

        scope.Aliases.Original(alias).ShouldBe("Dune");
    }

    [Fact]
    public async Task OriginalReturnsAValueThatIsNotAnAliasUnchanged()
    {
        await using var run = await TestSuite.StartAsync();
        await using var scope = run.CreateScope(channel: null, specName: "spec");

        scope.Aliases.Original("Neuromancer").ShouldBe("Neuromancer");
    }

    [Fact]
    public async Task UniqueGivesAFreshValueOnEachCall()
    {
        await using var run = await TestSuite.StartAsync();
        await using var scope = run.CreateScope(channel: null, specName: "spec");

        var first = scope.Aliases.Unique("order");
        var second = scope.Aliases.Unique("order");

        first.ShouldStartWith("order-");
        second.ShouldStartWith("order-");
        first.ShouldNotBe(second);
        scope.Aliases.Original(first).ShouldBe("order");
    }

    [Fact]
    public async Task TwoSpecsGetDifferentAliasesForTheSameName()
    {
        await using var run = await TestSuite.StartAsync();
        await using var one = run.CreateScope(channel: null, specName: "one");
        await using var two = run.CreateScope(channel: null, specName: "two");

        one.Aliases.For("Dune").ShouldNotBe(two.Aliases.For("Dune"));
    }
}
