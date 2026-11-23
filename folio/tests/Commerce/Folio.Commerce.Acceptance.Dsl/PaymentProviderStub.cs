using System.Diagnostics.CodeAnalysis;
using Lamina;
using Lamina.Stubs.WireMock;
using Microsoft.Extensions.DependencyInjection;

namespace Folio.Commerce.Acceptance.Dsl;

/// <summary>
/// Folio's card payment provider, as a WireMock stub. Payments creates a payment with
/// <c>POST /payments {amount, currency, cardToken, reference}</c>, the amount in pence. Spec rules are keyed on the
/// aliased card token in that body. The provider confirms a pending payment by posting
/// <c>{type: "payment.succeeded", id, reference}</c> to Payments' <c>/webhooks/payment-provider</c>.
/// </summary>
[SuppressMessage("Minor Code Smell", "S4261:Methods should be named according to their synchronicities", Justification = "Domain verbs, named by the issue and Plan.md §6.5 (WillDecline).")]
public sealed class PaymentProviderStub : WireMockStub
{
    private const string PaymentsPath = "/payments";
    private const string PaymentsResource = "payments";
    private const string WebhookPath = "/webhooks/payment-provider";
    private const int Created = 201;
    private const int PaymentRequired = 402;

    private static readonly Guid AcceptAnyCreateId = new("f0110000-0000-4000-8000-00000000c001");

    protected override string Resource => "payment-provider";

    /// <summary>
    /// Declines every payment created with the aliased <paramref name="card"/> token as <c>card_declined</c>.
    /// </summary>
    public Task WillDecline(string card) =>
        When(Create(card)).Respond(PaymentRequired, new { error = "card_declined" });

    /// <summary>
    /// Waits for the provider to accept a payment created with the aliased <paramref name="card"/> token, then confirms it
    /// by sending Payments the <c>payment.succeeded</c> webhook for that payment.
    /// </summary>
    public async Task ConfirmsPayment(string card)
    {
        var accepted = await Scope.Services.GetRequiredService<Eventually>().Until(
            async () => (await Journal(Create(card))).Where(e => e.Status == Created).ToList(),
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

    /// <summary>
    /// Run default: any payment create is taken, under a new provider id, and left pending until the provider confirms it.
    /// </summary>
    protected override Task InstallDefaults() =>
        Default(
            AcceptAnyCreateId,
            Post(PaymentsPath),
            new StubResponse(Created, new { id = "pay_{{Random Type=\"Guid\"}}", status = "pending" }) { Templated = true });

    private static StubRequest Create(string card) => Post(PaymentsPath).WithBodyField("cardToken", card);
}
