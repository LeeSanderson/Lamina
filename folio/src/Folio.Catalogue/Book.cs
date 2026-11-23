namespace Folio.Catalogue;

public sealed class Book
{
    public Guid Id { get; init; }

    public required string Title { get; init; }

    public decimal Price { get; init; }

    public int OnHand { get; init; }

    public int Reserved { get; init; }

    public int Available => OnHand - Reserved;
}
