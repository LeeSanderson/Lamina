using Lamina;

namespace Folio.Commerce.Acceptance.Dsl;

internal static class CommerceKeys
{
    internal static Key<string> Order { get; } = new("order");

    internal static Key<string> BookId(string title) => new($"book:{title}");
}
