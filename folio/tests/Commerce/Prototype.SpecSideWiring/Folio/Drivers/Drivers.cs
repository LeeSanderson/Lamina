// Folio's drivers and stubs (layer 3). Lives in Folio.Commerce.Acceptance.Drivers.
// The real ones use Lamina.Drivers.Http against the Web BFF and Lamina.Stubs.WireMock; these call the in-memory fake.
using Folio.Commerce.Acceptance.Dsl;
using Folio.Commerce.Acceptance.FakeSut;

namespace Folio.Commerce.Acceptance.Drivers;

public sealed class ApiCatalogueDriver(InMemoryFolio folio) : ICatalogueDriver
{
    public Task<string> AddBook(string aliasedTitle, int stock, decimal price) => Task.FromResult(folio.AddBook(aliasedTitle, stock, price));

    public Task<int> AvailableStock(string bookId) => Task.FromResult(folio.GetBook(bookId).Available);
}

public sealed class ApiShoppingDriver(InMemoryFolio folio) : IShoppingDriver
{
    public async Task<PlacedOrder> PlaceOrder(string aliasedCustomer, string bookId, string aliasedCard) =>
        ToPlaced(await folio.PlaceOrder(aliasedCustomer, bookId, aliasedCard));

    private int reads;

    // PROTOTYPE hack for the broken specs: a customer named "Outage..." sees the BFF answer 503 on the first three reads,
    // so Eventually's observation history shows the probe throwing, then a real (but wrong) state.
    public Task<PlacedOrder> GetOrder(string aliasedCustomer, string orderId) =>
        aliasedCustomer.StartsWith("Outage", StringComparison.Ordinal) && Interlocked.Increment(ref reads) <= 3
            ? throw new HttpRequestException("Response status code does not indicate success: 503 (Service Unavailable).")
            : Task.FromResult(ToPlaced(folio.GetOrder(orderId)));

    private static PlacedOrder ToPlaced(Order o) => new(o.Id, o.Status, o.Reason);
}
