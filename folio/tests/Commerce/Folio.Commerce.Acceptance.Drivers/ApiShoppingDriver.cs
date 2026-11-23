using Folio.Commerce.Acceptance.Dsl;

namespace Folio.Commerce.Acceptance.Drivers;

internal sealed class ApiShoppingDriver(ApiSession session, WebBffClient bff) : IShoppingDriver
{
    public Task SignInAsCustomerAsync(string name)
    {
        session.SignInAsCustomer(name);
        return Task.CompletedTask;
    }

    public Task SignInAsStaffAsync(string name)
    {
        session.SignInAsStaff(name);
        return Task.CompletedTask;
    }

    public async Task<string> PlaceOrderAsync(string bookId, int quantity, string cardToken)
    {
        var order = await bff.PostAsync<OrderView>("/api/orders", new PlaceOrderRequest(Guid.Parse(bookId), quantity, cardToken));
        return order.Id.ToString();
    }

    public async Task<PlacedOrder> ReadOrderAsync(string orderId)
    {
        var order = await bff.GetAsync<OrderView>($"/api/orders/{orderId}");
        return new PlacedOrder(order.Status, order.Reason);
    }

    private sealed record PlaceOrderRequest(Guid BookId, int Quantity, string CardToken);

    private sealed record OrderView(Guid Id, string Status, string? Reason);
}
