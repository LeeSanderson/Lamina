// PROTOTYPE specs for "Stub abstraction over WireMock" (issues/22): the parts of the stub API C1–C3 don't reach.
using Lamina.XunitV3;
using Xunit;
using static Folio.Commerce.Acceptance.Dsl.FolioChannels;

namespace Folio.Commerce.Acceptance.Specs;

[Channels(Api)]
public abstract class StubSpecs : FolioSpec
{
    // WillFail(status, times): answers 503 twice, then falls through to the run's default (201 pending).
    [Fact]
    public async Task ProviderUnavailableTwiceThenAccepts()
    {
        await Catalogue.AddBook(title: "Dune");
        await PaymentProvider.IsUnavailable(card: "visa", times: 2);

        await Shopping.PlaceOrder(title: "Dune", card: "visa");
        await Shopping.ConfirmOrderPending();

        await PaymentProvider.ConfirmsPayment(card: "visa");
        await Shopping.ConfirmOrderConfirmed();
        await PaymentProvider.ConfirmRetriedWithSameIdempotencyKey(card: "visa");
        await PaymentProvider.ConfirmCharged(card: "visa", pence: 999);
    }

    // The guard: a rule that isn't keyed to the spec is refused before it reaches WireMock.
    [Fact]
    public async Task UnaliasedRuleIsRefused()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => PaymentProvider.WillDeclineUnaliased(card: "visa"));
        Assert.Contains("isn't keyed to this spec", ex.Message, StringComparison.Ordinal);
    }

    // Isolation: the same card name in many parallel specs. Half decline, half don't; each sees only its own rule
    // and its own journal. Run with -parallelMode all -maxThreads unlimited.
    [Theory]
    [MemberData(nameof(Copies))]
    public async Task SameCardNameInParallel(int copy)
    {
        await Catalogue.AddBook(title: "Dune", stock: 1);
        if (copy % 2 == 0) await PaymentProvider.WillDecline(card: "visa");

        await Shopping.PlaceOrder(title: "Dune", card: "visa");

        if (copy % 2 == 0) await Shopping.ConfirmOrderRejected(reason: "payment declined");
        else await Shopping.ConfirmOrderPending();
        await PaymentProvider.ConfirmCharged(card: "visa", pence: 999);
    }

    public static TheoryData<int> Copies() => [.. Enumerable.Range(0, 40)];
}
