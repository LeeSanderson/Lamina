using System.Diagnostics.CodeAnalysis;
using Lamina;
using Lamina.Dsl;
using Shouldly;

namespace Folio.Commerce.Acceptance.Dsl;

[SuppressMessage("Minor Code Smell", "S4261:Methods should be named according to their synchronicities", Justification = "DSL methods read as domain language in specs.")]
public sealed class CatalogueDsl(ICatalogueDriver driver) : LaminaDsl
{
    public Task AddBook(string title) => AddBook(new BookBuilder(title));

    public Task AddBook(BookBuilder book)
    {
        ArgumentNullException.ThrowIfNull(book);
        return AddBookCore(book);
    }

    public async Task ConfirmInStock(string title, int availableQuantity)
    {
        var bookId = Recall(BookId(title));

        await Eventually.Until(
            () => driver.ReadBookAsync(bookId),
            book =>
            {
                book.Title.ShouldBe(Alias(title));
                book.AvailableQuantity.ShouldBe(availableQuantity);
            },
            TimeSpan.FromSeconds(5),
            $"{title} should have {availableQuantity} in stock");
    }

    private static Key<string> BookId(string title) => new($"book:{title}");

    private async Task AddBookCore(BookBuilder book)
    {
        var bookId = await driver.AddBookAsync(Alias(book.Title), book.Stock, book.Price);
        Remember(BookId(book.Title), bookId);
    }
}
