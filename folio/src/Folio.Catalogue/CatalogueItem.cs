namespace Folio.Catalogue;

/// <summary>
/// Placeholder so the solution builds until phase 1 adds the catalogue service.
/// </summary>
/// <param name="Sku">The stock-keeping unit.</param>
/// <param name="Title">The title shown to customers.</param>
public sealed record CatalogueItem(string Sku, string Title);
