using System.Diagnostics.CodeAnalysis;
using Lamina.Dsl;
using Shouldly;

namespace Folio.Commerce.Acceptance.Dsl;

[SuppressMessage("Minor Code Smell", "S4261:Methods should be named according to their synchronicities", Justification = "DSL methods read as domain language in specs.")]
public sealed class ShoppingDsl(IShoppingDriver driver) : LaminaDsl
{
    private const string Pending = "Pending";
    private const string Confirmed = "Confirmed";
    private const string Rejected = "Rejected";

    public Task AsCustomer(string name) => driver.SignInAsCustomerAsync(Alias(name));

    public Task AsStaff(string name) => driver.SignInAsStaffAsync(Alias(name));

    public async Task PlaceOrder(string title, string card)
    {
        var orderId = await driver.PlaceOrderAsync(Recall(CommerceKeys.BookId(title)), 1, Alias(card));
        Remember(CommerceKeys.Order, orderId);
    }

    public async Task ConfirmOrderPending()
    {
        var order = await driver.ReadOrderAsync(Recall(CommerceKeys.Order));
        order.Status.ShouldBe(Pending, "the order should be waiting for its payment");
    }

    public Task ConfirmOrderConfirmed()
    {
        var orderId = Recall(CommerceKeys.Order);
        return Eventually.Until(
            () => driver.ReadOrderAsync(orderId),
            order => order.Status.ShouldBe(Confirmed),
            TimeSpan.FromSeconds(10),
            $"the order should be {Confirmed}");
    }

    public async Task ConfirmOrderRejected(string reason)
    {
        var order = await driver.ReadOrderAsync(Recall(CommerceKeys.Order));
        order.ShouldSatisfyAllConditions(
            $"the order should be {Rejected} with {reason}",
            () => order.Status.ShouldBe(Rejected),
            () => order.Reason.ShouldBe(reason));
    }
}
