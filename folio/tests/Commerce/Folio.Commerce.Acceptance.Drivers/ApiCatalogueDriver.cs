using Folio.Commerce.Acceptance.Dsl;

namespace Folio.Commerce.Acceptance.Drivers;

internal sealed class ApiCatalogueDriver(WebBffClient bff) : ICatalogueDriver
{
    public async Task<string> AddBookAsync(string title, int stock, decimal price)
    {
        var added = await bff.PostAsync<BookAdded>("/api/staff/books", new AddBookRequest(title, price, stock));
        return added.Id.ToString();
    }

    public async Task<CatalogueBook> ReadBookAsync(string bookId)
    {
        var book = await bff.GetAsync<BookView>($"/api/books/{bookId}");
        return new CatalogueBook(book.Title, book.Price, book.AvailableQuantity);
    }

    private sealed record AddBookRequest(string Title, decimal Price, int Stock);

    private sealed record BookAdded(Guid Id);

    private sealed record BookView(Guid Id, string Title, decimal Price, int AvailableQuantity);
}
