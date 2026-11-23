using System.Diagnostics.CodeAnalysis;
using Lamina;
using Lamina.Stubs.WireMock;
using Microsoft.Extensions.DependencyInjection;

namespace Folio.Commerce.Acceptance.Dsl;

// Payments creates a payment with POST /payments {amount (pence), currency, cardToken, reference}; spec rules key on
// the aliased card token. The provider confirms one by posting {type: "payment.succeeded", id, reference} to
// Payments' /webhooks/payment-provider.
[SuppressMessage("Minor Code Smell", "S4261:Methods should be named according to their synchronicities", Justification = "Domain verbs, named by the issue and Plan.md §6.5 (WillDecline).")]
public sealed class PaymentProviderStub : WireMockStub
{
    private const string PaymentsPath = "/payments";
    private const string PaymentsResource = "payments";
    private const string WebhookPath = "/webhooks/payment-provider";
    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private const int Created = 201;
    private const int PaymentRequired = 402;

    private static readonly Guid AcceptAnyCreateId = new("f0110000-0000-4000-8000-00000000c001");

    protected override string Resource => "payment-provider";

    public Task WillDecline(string card) =>
        When(Create(card)).Respond(PaymentRequired, new { error = "card_declined" });

    // Payments gets a 201 with an empty body, so it can't tell whether the customer was charged.
    public Task LosesAnswer(string card, int times) =>
        When(Create(card)).WillAnswerEmpty(Created, times);

    public async Task<IReadOnlyList<string?>> CreateIdempotencyKeys(string card) =>
        [.. (await Journal(Create(card))).Select(e => e.Header(IdempotencyKeyHeader))];

    public async Task ConfirmsPayment(string card)
    {
        var accepted = await Scope.Services.GetRequiredService<Eventually>().Until(
            async () => (await Journal(Create(card))).Where(e => e.Status == Created && !e.AnswerLost).ToList(),
            creates => creates.Count > 0,
            TimeSpan.FromSeconds(10),
            $"the payment provider should have accepted a payment created with card {card}");
        var create = accepted[^1];
        var reference = create.Body?["reference"]?.GetValue<string>()
            ?? throw new InvalidOperationException($"The accepted payment create has no reference: {create}");
        var id = create.ResponseBody?["id"]?.GetValue<string>()
            ?? throw new InvalidOperationException($"The provider's answer to the payment create has no id: {create}");

        await SendWebhook(PaymentsResource, WebhookPath, new { type = "payment.succeeded", id, reference });
    }

    protected override Task InstallDefaults() =>
        Default(
            AcceptAnyCreateId,
            Post(PaymentsPath),
            new StubResponse(Created, new { id = "pay_{{Random Type=\"Guid\"}}", status = "pending" }) { Templated = true });

    private static StubRequest Create(string card) => Post(PaymentsPath).WithBodyField("cardToken", card);
}
