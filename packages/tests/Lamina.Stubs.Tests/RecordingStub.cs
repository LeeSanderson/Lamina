namespace Lamina.Stubs.Tests;

// A stand-in backend: it records what LaminaStub asks of it instead of talking to a real stub server.
internal sealed class RecordingStub(List<string> events) : LaminaStub
{
    public Uri WhereItLives => Endpoint;

    protected override string Resource => "payment-provider";

    public void AddRule(string matcher)
    {
        RequireKeyedToSpec($"rule '{matcher}'", matcher);
        TrackRule($"rule-for-{matcher}");
    }

    public void QueryJournal(string matcher) => RequireKeyedToSpec($"journal query '{matcher}'", matcher);

    public void AddDefault() => RequireRunStart("a default");

    public string AliasFor(string name) => Scope.Aliases.For(name);

    protected override Task InstallDefaults()
    {
        AddDefault();
        events.Add("defaults installed");
        return Task.CompletedTask;
    }

    protected override Task DeleteRulesAsync(IReadOnlyList<string> ids)
    {
        events.Add($"delete {string.Join(",", ids)}");
        return Task.CompletedTask;
    }
}
