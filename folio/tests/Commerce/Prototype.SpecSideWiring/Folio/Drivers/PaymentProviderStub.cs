// The team's typed stub (Plan.md §6.5): domain-aware, so it belongs to Folio. Lives in Folio.Commerce.Acceptance.Drivers.
// Everything WireMock-shaped is a protected building block from Lamina.Stubs.WireMock; this class only says
// what the payment provider does, in the provider's own terms.
//
// Provider contract (from "Folio Commerce behaviour for C1–C3"):
//   POST /payments { amount (pence), currency, cardToken, reference } + Idempotency-Key
//     -> 402 { error: "card_declined" }  or  201 { id, status: "pending" }
//   webhook to Payments: POST /webhooks/payment-provider { type: "payment.confirmed", reference, providerPaymentId }
using Lamina.Stubs.WireMock;

namespace Folio.Commerce.Acceptance.Drivers;

public sealed record PaymentCreate(string CardToken, string Reference, long Amount, string IdempotencyKey, int? Status, bool Lost, string? ProviderPaymentId)
{
    /// <summary>Accepted, and Payments got the answer.</summary>
    public bool Accepted => Status == 201 && !Lost;
}

public sealed class PaymentProviderStub : WireMockStub
{
    private static readonly Guid AcceptAnyCard = new("5e0c1b4a-0000-4000-8000-00000000a001");

    protected override string Resource => "payment-provider";

    // Every card is accepted (pending, then a webhook) unless a spec says otherwise.
    protected override Task InstallDefaults() =>
        Default(AcceptAnyCard, Post("/payments"), new StubResponse(201, new { id = "pay_{{Random Type=\"Guid\"}}", status = "pending" }) { Templated = true });

    public Task WillDecline(string aliasedCard) =>
        When(Create(aliasedCard)).Respond(402, new { error = "card_declined" });

    /// <summary>C3: the provider takes the payment, but the answer is lost: a 201 whose body never arrives.</summary>
    public Task LosesAnswer(string aliasedCard, int times) =>
        When(Create(aliasedCard)).WillAnswerEmpty(status: 201, times);

    public Task IsUnavailable(string aliasedCard, int times) =>
        When(Create(aliasedCard)).WillFail(status: 503, times);

    /// <summary>Every create the provider received for this card, oldest first, with what it answered.</summary>
    public async Task<IReadOnlyList<PaymentCreate>> Creates(string aliasedCard) =>
        (await Journal(Create(aliasedCard)))
            .Select(e => new PaymentCreate(
                CardToken: e.Body!["cardToken"]!.GetValue<string>(),
                Reference: e.Body["reference"]!.GetValue<string>(),
                Amount: e.Body["amount"]!.GetValue<long>(),
                IdempotencyKey: e.Header("Idempotency-Key") ?? "(none)",
                Status: e.Status,
                Lost: e.Fault is not null,
                ProviderPaymentId: e.ResponseBody?["id"]?.GetValue<string>()))
            .ToList();

    /// <summary>Sends the confirmation webhook for an accepted create, as the provider would.</summary>
    public Task SendConfirmed(PaymentCreate accepted) =>
        SendWebhook(endpoint: "payments", path: "/webhooks/payment-provider", new
        {
            type = "payment.confirmed",
            reference = accepted.Reference,
            providerPaymentId = accepted.ProviderPaymentId,
        });

    private static StubRequest Create(string aliasedCard) => Post("/payments").WithJson("cardToken", aliasedCard);
}
