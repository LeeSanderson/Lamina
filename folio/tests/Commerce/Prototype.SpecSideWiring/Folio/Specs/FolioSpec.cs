// The team's spec base: names the suite once, and exposes the DSLs as the spec's vocabulary.
// Lives in Folio.Commerce.Acceptance.Specs.
//
// Plan.md §6.2 writes `catalogue.AddBook(...)` with protected lowercase fields. The coding standards reject that
// (CA1051, visible instance fields), and the fields couldn't be set in the constructor anyway (the scope is created
// in InitializeAsync). So the vocabulary is PascalCase properties resolved from the spec scope.
using Folio.Commerce.Acceptance.Dsl;
using Lamina.XunitV3;

namespace Folio.Commerce.Acceptance.Specs;

public abstract class FolioSpec : LaminaSpec<FolioSuite>
{
    protected CatalogueDsl Catalogue => Dsl<CatalogueDsl>();

    protected ShoppingDsl Shopping => Dsl<ShoppingDsl>();

    protected PaymentProviderDsl PaymentProvider => Dsl<PaymentProviderDsl>();
}
