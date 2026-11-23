using Lamina;

namespace Folio.Commerce.Acceptance.Dsl;

/// <summary>
/// What Commerce DSLs remember for each other within a spec.
/// </summary>
internal static class CommerceKeys
{
    internal static Key<string> Order { get; } = new("order");

    internal static Key<string> BookId(string title) => new($"book:{title}");
}
