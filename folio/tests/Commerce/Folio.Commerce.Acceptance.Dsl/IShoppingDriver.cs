namespace Folio.Commerce.Acceptance.Dsl;

// Later calls on any driver act as the signed-in user.
public interface IShoppingDriver
{
    Task SignInAsCustomerAsync(string name);

    Task SignInAsStaffAsync(string name);

    Task<string> PlaceOrderAsync(string bookId, int quantity, string cardToken);

    Task<PlacedOrder> ReadOrderAsync(string orderId);
}
