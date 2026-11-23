using Lamina.XunitV3;

namespace Folio.Commerce.Acceptance.Specs;

[Channels(FolioChannels.Api)]
public abstract class CheckoutSpecs : FolioSpec
{
    [Fact]
    public async Task OrderIsRejectedAndStockReleasedWhenPaymentDeclined()
    {
        await Shopping.AsStaff("Sam");
        await Catalogue.AddBook(ABook("Dune").WithStock(1));
        await PaymentProvider.WillDecline(card: "amex");

        await Shopping.AsCustomer("Chris");
        await Shopping.PlaceOrder(title: "Dune", card: "amex");

        await Shopping.ConfirmOrderRejected(reason: "PaymentDeclined");
        await Catalogue.ConfirmInStock("Dune", 1);
    }
}
