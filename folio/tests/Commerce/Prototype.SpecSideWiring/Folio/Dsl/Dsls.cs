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
public sealed class PaymentProviderDsl(PaymentProviderStub stub) : LaminaDsl
{
    public Task WillDecline(string card) => stub.WillDecline(Alias(card));

    public Task DropsConnection(string card, int times = 1) => stub.DropsConnection(Alias(card), times);

    public async Task ConfirmsPayment(string card)
    {
        await Eventually.Until(() => stub.Creates(Alias(card)), c => c.Count > 0, within: TimeSpan.FromSeconds(5), because: $"the provider should receive a payment for {card}");
        await stub.SendConfirmedWebhook(Alias(card));
    }

    public async Task ConfirmRetriedWithSameIdempotencyKey(string card)
    {
        var creates = await Eventually.Until(() => stub.Creates(Alias(card)), c => c.Count >= 2, within: TimeSpan.FromSeconds(5), because: $"the payment for {card} should be retried");
        Assert.Single(creates.Select(c => c.IdempotencyKey).Distinct(StringComparer.Ordinal));
    }
}
