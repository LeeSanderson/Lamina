// C1–C3 as a spec writer sees them. Lives in Folio.Commerce.Acceptance.Specs.
using Lamina.XunitV3;
using Xunit;
using static Folio.Commerce.Acceptance.Dsl.FolioChannels;

namespace Folio.Commerce.Acceptance.Specs;

[Channels(Api)]
public abstract class CheckoutSpecs : FolioSpec
{
    [Fact]
    public async Task OrderIsRejectedAndStockReleasedWhenPaymentDeclined()
    {
        await Catalogue.AddBook(title: "Dune", stock: 1);
        await PaymentProvider.WillDecline(card: "visa-declined");

        await Shopping.PlaceOrder(title: "Dune", card: "visa-declined");

        await Shopping.ConfirmOrderRejected(reason: "payment declined");
        await Catalogue.ConfirmStock(title: "Dune", stock: 1);
    }

    [Fact]
    public async Task OrderIsConfirmedWhenProviderConfirmsPaymentByWebhook()
    {
        await Catalogue.AddBook(title: "Dune", stock: 1);

        await Shopping.PlaceOrder(title: "Dune", card: "visa");
        await Shopping.ConfirmOrderPending();

        await PaymentProvider.ConfirmsPayment(card: "visa");

        await Shopping.ConfirmOrderConfirmed();
        await Catalogue.ConfirmStock(title: "Dune", stock: 0);
    }

    [Fact]
    public async Task OrderStaysPendingWhenProvidersAnswerIsLostThenConfirmsOnRetry()
    {
        await Catalogue.AddBook(title: "Dune");
        await PaymentProvider.DropsConnection(card: "visa", times: 1);

        await Shopping.PlaceOrder(title: "Dune", card: "visa");
        await Shopping.ConfirmOrderPending();
        await PaymentProvider.ConfirmRetriedWithSameIdempotencyKey(card: "visa");

        await PaymentProvider.ConfirmsPayment(card: "visa");

        await Shopping.ConfirmOrderConfirmed();
    }
}
