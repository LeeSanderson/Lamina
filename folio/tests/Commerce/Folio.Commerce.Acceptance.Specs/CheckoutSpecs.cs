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

    [Fact]
    public async Task OrderIsConfirmedWhenTheProviderConfirmsPaymentByWebhook()
    {
        await Shopping.AsStaff("Sam");
        await Catalogue.AddBook(ABook("Dune").WithStock(2));

        await Shopping.AsCustomer("Chris");
        await Shopping.PlaceOrder(title: "Dune", card: "visa");
        await Shopping.ConfirmOrderPending();

        await PaymentProvider.ConfirmsPayment(card: "visa");

        await Shopping.ConfirmOrderConfirmed();
    }

    [Fact]
    public async Task OrderStaysPendingWhenTheProvidersAnswerIsLostThenConfirmsOnRetry()
    {
        await Shopping.AsStaff("Sam");
        await Catalogue.AddBook(ABook("Dune").WithStock(2));
        await PaymentProvider.WillLoseFirstAnswer(card: "visa");

        await Shopping.AsCustomer("Chris");
        await Shopping.PlaceOrder(title: "Dune", card: "visa");
        await Shopping.ConfirmOrderPending();

        await PaymentProvider.ConfirmPaymentCreatedWithTheSameKey(card: "visa", times: 2);
        await PaymentProvider.ConfirmsPayment(card: "visa");

        await Shopping.ConfirmOrderConfirmed();
    }

    [Fact]
    public async Task ARepeatedConfirmationWebhookCommitsTheStockOnce()
    {
        await Shopping.AsStaff("Sam");
        await Catalogue.AddBook(ABook("Dune").WithStock(2));

        await Shopping.AsCustomer("Chris");
        await Shopping.PlaceOrder(title: "Dune", card: "visa");

        await PaymentProvider.ConfirmsPayment(card: "visa");
        await PaymentProvider.ConfirmsPayment(card: "visa");

        await Shopping.ConfirmOrderConfirmed();
        await Catalogue.ConfirmInStock("Dune", 1);
    }
}
