using System.Diagnostics.CodeAnalysis;
using Lamina.Dsl;
using Shouldly;

namespace Folio.Commerce.Acceptance.Dsl;

[SuppressMessage("Minor Code Smell", "S4261:Methods should be named according to their synchronicities", Justification = "DSL methods read as domain language in specs.")]
public sealed class PaymentProviderDsl(PaymentProviderStub stub) : LaminaDsl
{
    /// <summary>
    /// The provider declines every payment the spec makes with <paramref name="card"/>.
    /// </summary>
    public Task WillDecline(string card) => stub.WillDecline(Alias(card));

    /// <summary>
    /// The provider takes the spec's first payment with <paramref name="card"/>, but its answer is lost on the way back.
    /// </summary>
    public Task WillLoseFirstAnswer(string card) => stub.LosesAnswer(Alias(card), times: 1);

    /// <summary>
    /// Waits until the provider has received exactly <paramref name="times"/> creates for the spec's payment with
    /// <paramref name="card"/>, all with the same Idempotency-Key, so the customer can't be charged twice.
    /// </summary>
    public Task ConfirmPaymentCreatedWithTheSameKey(string card, int times)
    {
        var token = Alias(card);
        return Eventually.Until(
            () => stub.CreateIdempotencyKeys(token),
            keys => keys.ShouldSatisfyAllConditions(
                () => keys.Count.ShouldBe(times),
                () => keys.ShouldAllBe(key => !string.IsNullOrEmpty(key) && key == keys[0])),
            TimeSpan.FromSeconds(10),
            $"the provider should have received {times} creates for card {card}, all with the same Idempotency-Key");
    }

    /// <summary>
    /// The provider confirms, by webhook, the payment the spec made with <paramref name="card"/>, once it has accepted it.
    /// </summary>
    public Task ConfirmsPayment(string card) => stub.ConfirmsPayment(Alias(card));
}
