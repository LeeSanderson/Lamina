using System.Diagnostics.CodeAnalysis;
using Lamina.Dsl;
using Shouldly;

namespace Folio.Commerce.Acceptance.Dsl;

[SuppressMessage("Minor Code Smell", "S4261:Methods should be named according to their synchronicities", Justification = "DSL methods read as domain language in specs.")]
public sealed class PaymentProviderDsl(PaymentProviderStub stub) : LaminaDsl
{
    public Task WillDecline(string card) => stub.WillDecline(Alias(card));

    public Task WillLoseFirstAnswer(string card) => stub.LosesAnswer(Alias(card), times: 1);

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

    public Task ConfirmsPayment(string card) => stub.ConfirmsPayment(Alias(card));
}
