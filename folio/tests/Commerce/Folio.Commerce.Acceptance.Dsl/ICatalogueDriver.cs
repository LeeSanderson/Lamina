namespace Folio.Commerce.Acceptance.Dsl;

// Acts as whoever the spec last signed in as.
public interface ICatalogueDriver
{
    Task<string> AddBookAsync(string title, int stock, decimal price);

    Task<CatalogueBook> ReadBookAsync(string bookId);
}
