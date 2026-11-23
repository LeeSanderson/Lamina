using System.Diagnostics.CodeAnalysis;
using Lamina.Stubs.WireMock;

namespace Folio.Commerce.Acceptance.Dsl;

/// <summary>
/// Folio's card payment provider, as a WireMock stub. Payments creates a payment with
/// <c>POST /payments {amount, currency, cardToken, reference}</c>, the amount in pence. Spec rules are keyed on the
/// aliased card token in that body.
/// </summary>
[SuppressMessage("Minor Code Smell", "S4261:Methods should be named according to their synchronicities", Justification = "Domain verbs, named by the issue and Plan.md §6.5 (WillDecline).")]
public sealed class PaymentProviderStub : WireMockStub
{
    private const string PaymentsPath = "/payments";
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
    /// Run default: any payment create is taken and left pending until the provider confirms it.
    /// </summary>
    protected override Task InstallDefaults() =>
        Default(AcceptAnyCreateId, Post(PaymentsPath), new StubResponse(Created, new { id = "pay_default", status = "pending" }));

    private static StubRequest Create(string card) => Post(PaymentsPath).WithBodyField("cardToken", card);
}
