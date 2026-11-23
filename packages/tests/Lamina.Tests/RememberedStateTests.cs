namespace Lamina.Tests;

public sealed class RememberedStateTests
{
    private static readonly Key<Guid> LastOrder = new("last order");

    [Fact]
    public async Task AValueRememberedInASpecIsRecalledTypedInTheSameSpec()
    {
        await using var run = await TestSuite.StartAsync();
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var order = Guid.NewGuid();

        scope.Remember(LastOrder, order);

        scope.Recall(LastOrder).ShouldBe(order);
    }

    [Fact]
    public async Task AnotherSpecCannotRecallIt()
    {
        await using var run = await TestSuite.StartAsync();
        await using var one = run.CreateScope(channel: null, specName: "one");
        await using var two = run.CreateScope(channel: null, specName: "two");

        one.Remember(LastOrder, Guid.NewGuid());

        Should.Throw<InvalidOperationException>(() => two.Recall(LastOrder)).Message.ShouldContain("last order");
    }

    [Fact]
    public async Task RecallWithADefaultGivesTheDefaultWhenNothingIsRemembered()
    {
        await using var run = await TestSuite.StartAsync();
        await using var scope = run.CreateScope(channel: null, specName: "spec");

        scope.Recall(LastOrder, Guid.Empty).ShouldBe(Guid.Empty);
    }

    [Fact]
    public async Task RecallingUnderTheSameNameAsAnotherTypeThrows()
    {
        await using var run = await TestSuite.StartAsync();
        await using var scope = run.CreateScope(channel: null, specName: "spec");

        scope.Remember(new Key<string>("last order"), "not a guid");

        Should.Throw<InvalidOperationException>(() => scope.Recall(LastOrder)).Message.ShouldContain("String");
    }
}
