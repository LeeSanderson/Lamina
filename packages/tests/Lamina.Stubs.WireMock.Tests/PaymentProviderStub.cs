namespace Lamina.Stubs.WireMock.Tests;

// A team stub as Folio would write it: domain verbs over WireMockStub's protected building blocks.
public sealed class PaymentProviderStub : WireMockStub
{
    public static readonly Guid AcceptAnyCardId = new("5e0c1b4a-0000-4000-8000-00000000a001");

    protected override string Resource => "payment-provider";

    public string Card(string name) => Scope.Aliases.For(name);

    public Task WillDecline(string card) =>
        When(Create(card)).Respond(402, new { error = "card_declined" });

    public Task WillAccept(string card, string paymentId) =>
        When(Create(card)).Respond(201, new { id = paymentId, status = "pending" });

    public Task WillDeclineAnyCard() =>
        When(Post("/payments")).Respond(402);

    public Task AcceptAnyCard() =>
        Default(AcceptAnyCardId, Post("/payments"), new StubResponse(201, new { id = "pay_default", status = "pending" }));

    public Task<IReadOnlyList<JournalEntry>> Creates(string card) => Journal(Create(card));

    public Task<IReadOnlyList<JournalEntry>> AnyCreates() => Journal(Post("/payments"));

    protected override Task InstallDefaults() => AcceptAnyCard();

    private static StubRequest Create(string card) => Post("/payments").WithBodyField("cardToken", card);
}
