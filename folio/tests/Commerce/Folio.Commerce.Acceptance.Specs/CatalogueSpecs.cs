using Lamina.XunitV3;

namespace Folio.Commerce.Acceptance.Specs;

[Channels(FolioChannels.Api)]
public abstract class CatalogueSpecs : FolioSpec
{
    [Fact]
    public async Task StaffAddsABookAndACustomerSeesItInStock()
    {
        await Shopping.AsStaff("Sam");
        await Catalogue.AddBook(ABook("Dune").WithStock(1));

        await Shopping.AsCustomer("Chris");

        await Catalogue.ConfirmInStock("Dune", 1);
    }
}
