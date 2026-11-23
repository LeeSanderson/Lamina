using System.Diagnostics.CodeAnalysis;
using Lamina.Dsl;

namespace Folio.Commerce.Acceptance.Dsl;

[SuppressMessage("Minor Code Smell", "S4261:Methods should be named according to their synchronicities", Justification = "DSL methods read as domain language in specs.")]
public sealed class PaymentProviderDsl(PaymentProviderStub stub) : LaminaDsl
{
    /// <summary>
    /// The provider declines every payment the spec makes with <paramref name="card"/>.
    /// </summary>
    public Task WillDecline(string card) => stub.WillDecline(Alias(card));

    /// <summary>
    /// The provider confirms, by webhook, the payment the spec made with <paramref name="card"/>, once it has accepted it.
    /// </summary>
    public Task ConfirmsPayment(string card) => stub.ConfirmsPayment(Alias(card));
}
