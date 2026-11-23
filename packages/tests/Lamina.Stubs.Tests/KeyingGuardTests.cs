namespace Lamina.Stubs.Tests;

public sealed class KeyingGuardTests
{
    [Fact]
    public async Task ARuleWithoutTheTestIdIsRefusedWithAnExplanation()
    {
        var (_, run) = await StubSuite.StartAsync();
        await using var _ = run;
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var stub = scope.Services.GetRequiredService<RecordingStub>();

        var refused = Should.Throw<InvalidOperationException>(() => stub.AddRule("cardToken=visa"));

        refused.Message.ShouldContain("rule 'cardToken=visa'");
        refused.Message.ShouldContain($"isn't keyed to this spec (test {scope.TestId})");
        refused.Message.ShouldContain("aliased value");
    }

    [Fact]
    public async Task AJournalQueryWithoutTheTestIdIsRefused()
    {
        var (_, run) = await StubSuite.StartAsync();
        await using var _ = run;
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var stub = scope.Services.GetRequiredService<RecordingStub>();

        Should.Throw<InvalidOperationException>(() => stub.QueryJournal("cardToken=visa"))
            .Message.ShouldContain("journal query 'cardToken=visa'");
    }

    [Fact]
    public async Task ARuleOnAnAliasedValueIsAccepted()
    {
        var (_, run) = await StubSuite.StartAsync();
        await using var _ = run;
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var stub = scope.Services.GetRequiredService<RecordingStub>();

        Should.NotThrow(() => stub.AddRule($"cardToken={stub.AliasFor("visa")}"));
    }
}
