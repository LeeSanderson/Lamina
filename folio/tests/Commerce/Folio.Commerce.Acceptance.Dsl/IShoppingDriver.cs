namespace Folio.Commerce.Acceptance.Dsl;

/// <summary>
/// Signs in to Folio and places orders. Later calls on any driver act as the signed-in user.
/// </summary>
public interface IShoppingDriver
{
    Task SignInAsCustomerAsync(string name);

    Task SignInAsStaffAsync(string name);

    /// <summary>
    /// Places an order and returns its ID.
    /// </summary>
    Task<string> PlaceOrderAsync(string bookId, int quantity, string cardToken);

    Task<PlacedOrder> ReadOrderAsync(string orderId);
}
