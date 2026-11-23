using Lamina.Stubs.WireMock;

namespace Lamina.IntegrationTests;

public sealed class SmokeStub : WireMockStub
{
    public static readonly Guid AcceptAnyCardId = new("5e0c1b4a-0000-4000-8000-0000000051a0");

    protected override string Resource => "payment-provider";

    public string Card(string name) => Scope.Aliases.For(name);

    public Task WillDecline(string card) => When(Create(card)).Respond(402, new { error = "card_declined" });

    public Task<IReadOnlyList<JournalEntry>> Creates(string card) => Journal(Create(card));

    protected override Task InstallDefaults() =>
        Default(AcceptAnyCardId, Post(PaymentsPath), new StubResponse(201, new { id = "pay_default", status = "pending" }));

    private const string PaymentsPath = "/payments";

    private static StubRequest Create(string card) => Post(PaymentsPath).WithBodyField("cardToken", card);
}
