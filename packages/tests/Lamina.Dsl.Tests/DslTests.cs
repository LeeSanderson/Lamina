namespace Lamina.Dsl.Tests;

public sealed class DslTests
{
    [Fact]
    public async Task ADslWithOnlyConstructorDependenciesGetsItsSpecsScope()
    {
        await using var run = await DslSuite.StartAsync();
        await using var scope = run.CreateScope(channel: null, specName: "spec");

        var dsl = scope.Services.GetRequiredService<CatalogueDsl>();

        dsl.AddBook("Dune").ShouldBe($"Dune-{scope.TestId}");
        scope.Services.GetRequiredService<ICatalogue>().Has($"Dune-{scope.TestId}").ShouldBeTrue();
    }

    [Fact]
    public async Task OneSpecGetsOneInstance()
    {
        await using var run = await DslSuite.StartAsync();
        await using var scope = run.CreateScope(channel: null, specName: "spec");

        scope.Services.GetRequiredService<CatalogueDsl>().ShouldBeSameAs(scope.Services.GetRequiredService<CatalogueDsl>());
    }

    [Fact]
    public async Task TwoSpecsGetTwoInstancesEachWithItsOwnScope()
    {
        await using var run = await DslSuite.StartAsync();
        await using var one = run.CreateScope(channel: null, specName: "one");
        await using var two = run.CreateScope(channel: null, specName: "two");

        var first = one.Services.GetRequiredService<CatalogueDsl>();
        var second = two.Services.GetRequiredService<CatalogueDsl>();

        first.ShouldNotBeSameAs(second);
        first.AddBook("Dune").ShouldBe($"Dune-{one.TestId}");
        second.AddBook("Dune").ShouldBe($"Dune-{two.TestId}");
    }

    [Fact]
    public async Task ADslRemembersAndRecallsWithinItsSpecOnly()
    {
        await using var run = await DslSuite.StartAsync();
        await using var one = run.CreateScope(channel: null, specName: "one");
        await using var two = run.CreateScope(channel: null, specName: "two");

        var added = one.Services.GetRequiredService<CatalogueDsl>().AddBook("Dune");

        one.Services.GetRequiredService<CatalogueDsl>().LastBookAdded().ShouldBe(added);
        two.Services.GetRequiredService<CatalogueDsl>().LastBookAddedOr("nothing").ShouldBe("nothing");
    }
}
