namespace Lamina.Dsl.Tests;

public sealed class CatalogueDsl(ICatalogue catalogue) : LaminaDsl
{
    private static readonly Key<string> LastBook = new("last book");

    public string AddBook(string title)
    {
        var aliased = Alias(title);
        catalogue.Add(aliased);
        Remember(LastBook, aliased);
        return aliased;
    }

    public string LastBookAdded() => Recall(LastBook);

    public string LastBookAddedOr(string fallback) => Recall(LastBook, fallback);
}
