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

    public Task WillFail(string card, int status, int times) =>
        When(Create(card)).WillFail(status, times);

    public Task LosesAnswer(string card, int times) =>
        When(Create(card)).WillAnswerEmpty(201, times);

    public Task AnswersGarbage(string card, int times) =>
        When(Create(card)).WillAnswerMalformed(201, times);

    public Task NeverAnswers(string card) =>
        When(Create(card)).WillTimeOut();

    public Task AcceptsSlowly(string card, TimeSpan delay) =>
        When(Create(card)).WillDelay(delay, new StubResponse(201, new { id = "pay_slow", status = "pending" }));

    public Task LosesAnyAnswer(int times) =>
        When(Post("/payments")).WillAnswerEmpty(201, times);

    public Task WillDeclineAnyCard() =>
        When(Post("/payments")).Respond(402);

    public Task AcceptAnyCard() =>
        Default(AcceptAnyCardId, Post("/payments"), new StubResponse(201, new { id = "pay_default", status = "pending" }));

    public Task<IReadOnlyList<JournalEntry>> Creates(string card) => Journal(Create(card));

    public Task<IReadOnlyList<JournalEntry>> AnyCreates() => Journal(Post("/payments"));

    // As Folio's ConfirmsPayment starts: wait for the provider to have taken a create before acting on it.
    public async Task<JournalEntry> AcceptedCreate(string card, TimeSpan within) =>
        (await Scope.Services.GetRequiredService<Eventually>().Until(
            () => Creates(card),
            creates => creates.Any(c => c.Status == 201),
            within,
            $"the provider should have accepted a payment created with card {card}"))[^1];

    protected override Task InstallDefaults() => AcceptAnyCard();

    private static StubRequest Create(string card) => Post("/payments").WithBodyField("cardToken", card);
}
