namespace Folio.Commerce.Acceptance.Dsl;

/// <summary>
/// Adds and reads books in Folio's catalogue, as whoever the spec last signed in as.
/// </summary>
public interface ICatalogueDriver
{
    Task<string> AddBookAsync(string title, int stock, decimal price);

    Task<CatalogueBook> ReadBookAsync(string bookId);
}
