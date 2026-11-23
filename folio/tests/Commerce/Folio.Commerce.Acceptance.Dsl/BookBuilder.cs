namespace Folio.Commerce.Acceptance.Dsl;

// Defaults for everything a spec doesn't mention.
public sealed class BookBuilder(string title)
{
    public string Title { get; } = title;

    public int Stock { get; private set; } = 10;

    public decimal Price { get; private set; } = 9.99m;

    public BookBuilder WithStock(int stock)
    {
        Stock = stock;
        return this;
    }

    public BookBuilder WithPrice(decimal price)
    {
        Price = price;
        return this;
    }
}
