using System.Net;
using System.Text;

namespace Folio.Payments.Tests;

public sealed class PaymentProviderClientTests
{
    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{ "id": "pay_1", "status": "succeeded" }""")]
    public async Task AnUnreadableTwoHundredAndOneIsAnUnknownOutcome(string body)
    {
        await using var provider = FakeProvider.Answering(_ => Respond(HttpStatusCode.Created, body));

        var reply = await provider.Client.CreatePaymentAsync(APayment(), TestContext.Current.CancellationToken);

        reply.Answer.ShouldBe(ProviderAnswer.Unknown);
    }

    [Fact]
    public async Task ACardDeclinedAnswerIsADecline()
    {
        await using var provider = FakeProvider.Answering(_ =>
            Respond(HttpStatusCode.PaymentRequired, """{ "error": { "code": "card_declined" } }"""));

        var reply = await provider.Client.CreatePaymentAsync(APayment(), TestContext.Current.CancellationToken);

        reply.Answer.ShouldBe(ProviderAnswer.Declined);
    }

    [Fact]
    public async Task APendingAnswerIsPendingWithTheProvidersPaymentId()
    {
        await using var provider = FakeProvider.Answering(_ =>
            Respond(HttpStatusCode.Created, """{ "id": "pay_8f2k", "status": "pending" }"""));

        var reply = await provider.Client.CreatePaymentAsync(APayment(), TestContext.Current.CancellationToken);

        reply.ShouldBe(new ProviderReply(ProviderAnswer.Pending, "pay_8f2k"));
    }

    [Fact]
    public async Task TheCreateCarriesTheAmountInPenceTheCardTheReferenceAndTheIdempotencyKey()
    {
        await using var provider = FakeProvider.Answering(_ =>
            Respond(HttpStatusCode.Created, """{ "id": "pay_8f2k", "status": "pending" }"""));

        await provider.Client.CreatePaymentAsync(APayment(), TestContext.Current.CancellationToken);

        var request = provider.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.RequestUri.ShouldBe(new Uri("http://payment-provider/payments"));
        request.Headers.GetValues("Idempotency-Key").ShouldBe(["0196f3a4-5b6c-7d8e-9f00-112233445566"]);
        provider.RequestBodies.ShouldHaveSingleItem().ShouldBe(
            """{"amount":2599,"currency":"GBP","cardToken":"visa-k3f9","reference":"pay-ref-1"}""");
    }

    [Fact]
    public async Task ADroppedConnectionIsAnUnknownOutcome()
    {
        await using var provider = FakeProvider.Answering(DropTheConnection);

        var reply = await provider.Client.CreatePaymentAsync(APayment(), TestContext.Current.CancellationToken);

        reply.Answer.ShouldBe(ProviderAnswer.Unknown);
    }

    [Fact]
    public async Task AFailedCreateIsSentOnceAndLeftForTheRetryWorker()
    {
        await using var provider = FakeProvider.Answering(_ => Respond(HttpStatusCode.ServiceUnavailable, string.Empty));

        var reply = await provider.Client.CreatePaymentAsync(APayment(), TestContext.Current.CancellationToken);

        reply.Answer.ShouldBe(ProviderAnswer.Unknown);
        provider.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AProviderThatDoesNotAnswerWithinTheTimeoutIsAnUnknownOutcome()
    {
        await using var provider = FakeProvider.Answering(NeverAnswerAsync, providerTimeout: TimeSpan.FromMilliseconds(200));

        var reply = await provider.Client.CreatePaymentAsync(APayment(), TestContext.Current.CancellationToken);

        reply.Answer.ShouldBe(ProviderAnswer.Unknown);
    }

    private static HttpResponseMessage DropTheConnection(CancellationToken cancellationToken) =>
        throw new HttpRequestException("The connection was closed.");

    private static async Task<HttpResponseMessage> NeverAnswerAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.Created);
    }

    private static PaymentCreate APayment() =>
        new("pay-ref-1", Guid.Parse("0196f3a4-5b6c-7d8e-9f00-112233445566"), 2599, "visa-k3f9");

    private static HttpResponseMessage Respond(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
