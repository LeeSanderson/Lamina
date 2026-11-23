namespace Lamina.Stubs.Tests;

public sealed class CleanupTests
{
    [Fact]
    public async Task WhenASpecEndsItsRulesAreDeletedByIdInOneCall()
    {
        var (suite, run) = await StubSuite.StartAsync();
        await using var _ = run;
        string visa;
        string amex;
        await using (var scope = run.CreateScope(channel: null, specName: "spec"))
        {
            var stub = scope.Services.GetRequiredService<RecordingStub>();
            visa = stub.AliasFor("visa");
            amex = stub.AliasFor("amex");
            stub.AddRule(visa);
            stub.AddRule(amex);
        }

        Deletes(suite).ShouldBe([$"delete rule-for-{visa},rule-for-{amex}"]);
    }

    [Fact]
    public async Task ASpecThatCreatedNoRulesSendsNoDelete()
    {
        var (suite, run) = await StubSuite.StartAsync();
        await using var _ = run;
        await using (var scope = run.CreateScope(channel: null, specName: "spec"))
        {
            var stub = scope.Services.GetRequiredService<RecordingStub>();
            stub.QueryJournal(stub.AliasFor("visa"));
        }

        Deletes(suite).ShouldBeEmpty();
    }

    [Fact]
    public async Task EachSpecDeletesOnlyItsOwnRules()
    {
        var (suite, run) = await StubSuite.StartAsync();
        await using var _ = run;
        await using var first = run.CreateScope(channel: null, specName: "first");
        var firstStub = first.Services.GetRequiredService<RecordingStub>();
        firstStub.AddRule(firstStub.AliasFor("visa"));
        string secondVisa;
        await using (var second = run.CreateScope(channel: null, specName: "second"))
        {
            var secondStub = second.Services.GetRequiredService<RecordingStub>();
            secondVisa = secondStub.AliasFor("visa");
            secondStub.AddRule(secondVisa);
        }

        Deletes(suite).ShouldBe([$"delete rule-for-{secondVisa}"]);
    }

    [Fact]
    public async Task ARuleRefusedByTheGuardIsNotTracked()
    {
        var (suite, run) = await StubSuite.StartAsync();
        await using var _ = run;
        await using (var scope = run.CreateScope(channel: null, specName: "spec"))
        {
            var stub = scope.Services.GetRequiredService<RecordingStub>();
            Should.Throw<InvalidOperationException>(() => stub.AddRule("visa"));
        }

        Deletes(suite).ShouldBeEmpty();
    }

    private static List<string> Deletes(StubSuite suite) =>
        [.. suite.Events.Where(e => e.StartsWith("delete", StringComparison.Ordinal))];
}
