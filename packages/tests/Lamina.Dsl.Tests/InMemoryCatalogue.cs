namespace Lamina.Dsl.Tests;

public sealed class InMemoryCatalogue : ICatalogue
{
    private readonly HashSet<string> _titles = new(StringComparer.Ordinal);

    public void Add(string title) => _titles.Add(title);

    public bool Has(string title) => _titles.Contains(title);
}
