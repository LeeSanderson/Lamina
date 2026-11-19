// Folio's DSLs (layer 2). Lives in Folio.Commerce.Acceptance.Dsl.
using Folio.Commerce.Acceptance.Drivers;
using Lamina;
using Lamina.Dsl;
using Xunit;

namespace Folio.Commerce.Acceptance.Dsl;

internal static class Remembered
{
    public static Key<string> Book(string title) => new($"book:{title}");

    public static readonly Key<string> LastOrder = new("last-order");

    public static readonly Key<string> Customer = new("customer");
}

[ArrangementDsl]
public sealed class CatalogueDsl(ICatalogueDriver driver) : LaminaDsl
{
    public async Task AddBook(string title, int stock = 10, decimal price = 9.99m) =>
        Remember(Remembered.Book(title), await driver.AddBook(Alias(title), stock, price));

    public async Task ConfirmStock(string title, int stock) =>
        Assert.Equal(stock, await driver.AvailableStock(Recall(Remembered.Book(title))));
}

public sealed class ShoppingDsl(IShoppingDriver driver) : LaminaDsl
{
    public ShoppingDsl AsCustomer(string name)
    {
        Remember(Remembered.Customer, name);
        return this;
    }

    public async Task PlaceOrder(string title, string card)
    {
        var order = await driver.PlaceOrder(Alias(Customer), Recall(Remembered.Book(title)), Alias(card));
        Remember(Remembered.LastOrder, order.Id);
    }

    public async Task ConfirmOrderRejected(string reason) =>
        Assert.Equal(("Rejected", reason), Shape(await LastOrder()));

    public async Task ConfirmOrderPending() =>
        Assert.Equal("Pending", (await LastOrder()).Status);

    public Task ConfirmOrderConfirmed() =>
        Eventually.Until(LastOrder, o => o.Status == "Confirmed", within: TimeSpan.FromSeconds(5), because: "the order should be confirmed");

    private string Customer => Recall(Remembered.Customer, orDefault: "Alice");

    private Task<PlacedOrder> LastOrder() => driver.GetOrder(Alias(Customer), Recall(Remembered.LastOrder));

    private static (string Status, string? Reason) Shape(PlacedOrder o) =>
        (o.Status, o.Reason switch { "PaymentDeclined" => "payment declined", var other => other });
}

// A DSL over a stub, not over a capability: stubs don't vary by channel, so the DSL takes the team's typed stub directly.
// Aliasing happens here, as for drivers: the stub only ever sees aliased values, and Lamina refuses a rule that isn't.
public sealed class PaymentProviderDsl(PaymentProviderStub stub) : LaminaDsl
{
    public Task WillDecline(string card) => stub.WillDecline(Alias(card));

    public Task DropsConnection(string card, int times) => stub.LosesAnswer(Alias(card), times);

    public Task IsUnavailable(string card, int times) => stub.IsUnavailable(Alias(card), times);

    // Wait until the provider has accepted a create for this card, then confirm THAT payment, taking its reference
    // and provider ID from the journal. The spec never needs to know the order ID.
    public async Task ConfirmsPayment(string card)
    {
        var creates = await Eventually.Until(
            () => stub.Creates(Alias(card)),
            c => c.Any(x => x.Accepted),
            within: TimeSpan.FromSeconds(5),
            because: $"the provider should accept a payment for {card}");
        await stub.SendConfirmed(creates.Last(x => x.Accepted));
    }

    public async Task ConfirmRetriedWithSameIdempotencyKey(string card)
    {
        // Assertion-lambda shape: the assertion's own message becomes the observation when time runs out.
        await Eventually.Until(
            () => stub.Creates(Alias(card)),
            creates =>
            {
                Assert.True(creates.Count >= 2, $"expected a retry, saw {creates.Count} create(s)");
                Assert.Single(creates.Select(c => c.IdempotencyKey).Distinct(StringComparer.Ordinal));
            },
            within: TimeSpan.FromSeconds(5),
            because: $"the payment for {card} should be retried with the same idempotency key");
    }

    public async Task ConfirmCharged(string card, long pence) =>
        Assert.All(await stub.Creates(Alias(card)), c => Assert.Equal(pence, c.Amount));

    // PROTOTYPE only, to show the guard: a rule on a raw (un-aliased) card would match every parallel spec's traffic.
    public Task WillDeclineUnaliased(string card) => stub.WillDecline(card);
}
