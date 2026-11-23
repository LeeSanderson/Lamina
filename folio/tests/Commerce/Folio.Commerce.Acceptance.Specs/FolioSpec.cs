using Folio.Commerce.Acceptance.Dsl;
using Lamina.XunitV3;

namespace Folio.Commerce.Acceptance.Specs;

public abstract class FolioSpec : LaminaSpec<FolioSuite>
{
    protected CatalogueDsl Catalogue => Dsl<CatalogueDsl>();

    protected ShoppingDsl Shopping => Dsl<ShoppingDsl>();

    protected static BookBuilder ABook(string title) => new(title);
}
