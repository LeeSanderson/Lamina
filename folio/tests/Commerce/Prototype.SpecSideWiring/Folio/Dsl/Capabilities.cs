// Layer 2/3 seam: the capabilities Folio's DSLs need. Lives in Folio.Commerce.Acceptance.Dsl.
namespace Folio.Commerce.Acceptance.Dsl;

public static class FolioChannels
{
    public const string Api = "api";
}

public interface ICatalogueDriver
{
    Task<string> AddBook(string aliasedTitle, int stock, decimal price);

    Task<int> AvailableStock(string bookId);
}

public interface IShoppingDriver
{
    Task<PlacedOrder> PlaceOrder(string aliasedCustomer, string bookId, string aliasedCard);

    Task<PlacedOrder> GetOrder(string aliasedCustomer, string orderId);
}

public sealed record PlacedOrder(string Id, string Status, string? Reason);
