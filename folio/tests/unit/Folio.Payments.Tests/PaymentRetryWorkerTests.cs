using System.Diagnostics;
using System.Net;
using System.Text;

namespace Folio.Payments.Tests;

public sealed class PaymentRetryWorkerTests
{
    private static readonly TimeSpan Millisecond = TimeSpan.FromMilliseconds(1);

    [Fact]
    public async Task ALostPaymentIsResubmittedBackingOffFrom50MsDoublingForFiveAttemptsThenTheOrderIsRejectedAsPaymentFailed()
    {
        await using var harness = RetryHarness.ProviderAnswering(() => Answer(HttpStatusCode.ServiceUnavailable, string.Empty));
        var lostAt = harness.Clock.GetUtcNow();
        var payment = await harness.ALostPaymentAsync();
        TimeSpan[] backOffs =
        [
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(400),
            TimeSpan.FromMilliseconds(800),
        ];

        var due = lostAt;
        foreach (var backOff in backOffs)
        {
            due += backOff;
            harness.Clock.Advance(due - harness.Clock.GetUtcNow() - Millisecond);
            (await harness.Worker.RetryDueAsync(TestContext.Current.CancellationToken)).ShouldBe(due, "the next attempt should be due after the back-off");
            harness.Clock.Advance(Millisecond);
            await harness.Worker.RetryDueAsync(TestContext.Current.CancellationToken);
        }

        harness.ProviderCallTimes.ShouldBe([.. backOffs.Select((_, n) => lostAt + backOffs.Take(n + 1).Aggregate(TimeSpan.Zero, (sum, b) => sum + b))]);
        harness.ProviderRequests.Select(r => r.Headers.GetValues("Idempotency-Key").Single()).ShouldAllBe(key => key == payment.IdempotencyKey.ToString());
        harness.OrderingCalls.ShouldBe([($"/orders/{payment.OrderId}/payment-outcome", """{"outcome":"Failed"}""")]);
        (await harness.ReadAsync(payment.Id)).Outcome.ShouldBe(PaymentOutcome.Failed);
        harness.Clock.Advance(TimeSpan.FromMinutes(5));
        (await harness.Worker.RetryDueAsync(TestContext.Current.CancellationToken)).ShouldBeNull();
        harness.ProviderRequests.Count.ShouldBe(backOffs.Length);
    }

    [Fact]
    public async Task AResubmissionTheProviderTakesLeavesThePaymentPendingAndStopsRetrying()
    {
        await using var harness = RetryHarness.ProviderAnswering(() => Answer(HttpStatusCode.Created, """{ "id": "pay_8f2k", "status": "pending" }"""));
        var payment = await harness.ALostPaymentAsync();

        harness.Clock.Advance(RetryHarness.FirstBackOff);
        var next = await harness.Worker.RetryDueAsync(TestContext.Current.CancellationToken);

        next.ShouldBeNull();
        var retried = await harness.ReadAsync(payment.Id);
        retried.Outcome.ShouldBe(PaymentOutcome.Pending);
        retried.ProviderPaymentId.ShouldBe("pay_8f2k");
        harness.OrderingCalls.ShouldBeEmpty();
    }

    [Fact]
    public async Task AResubmissionTheProviderDeclinesRejectsTheOrderAsDeclined()
    {
        await using var harness = RetryHarness.ProviderAnswering(() => Answer(HttpStatusCode.PaymentRequired, """{ "error": "card_declined" }"""));
        var payment = await harness.ALostPaymentAsync();

        harness.Clock.Advance(RetryHarness.FirstBackOff);
        await harness.Worker.RetryDueAsync(TestContext.Current.CancellationToken);

        (await harness.ReadAsync(payment.Id)).Outcome.ShouldBe(PaymentOutcome.Declined);
        harness.OrderingCalls.ShouldBe([($"/orders/{payment.OrderId}/payment-outcome", """{"outcome":"Declined"}""")]);
    }

    [Fact]
    public async Task APaymentTheWebhookConfirmsWhileItsResubmissionIsInFlightStaysConfirmed()
    {
        Guid paymentId = default;
        await using var harness = RetryHarness.ProviderAnswering(async h =>
        {
            await h.ConfirmAsTheWebhookDoesAsync(paymentId, "pay_8f2k");
            return Answer(HttpStatusCode.Created, """{ "id": "pay_8f2k", "status": "pending" }""");
        });
        paymentId = (await harness.ALostPaymentAsync()).Id;

        harness.Clock.Advance(RetryHarness.FirstBackOff);
        var next = await harness.Worker.RetryDueAsync(TestContext.Current.CancellationToken);

        next.ShouldBeNull();
        (await harness.ReadAsync(paymentId)).Outcome.ShouldBe(PaymentOutcome.Confirmed);
        harness.OrderingCalls.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheRunningWorkerWokenByPaymentsResubmitsOnceTheBackOffEnds()
    {
        await using var harness = RetryHarness.ProviderAnswering(() => Answer(HttpStatusCode.Created, """{ "id": "pay_8f2k", "status": "pending" }"""));
        await harness.StartAsync();
        var payment = await harness.ALostPaymentAsync();

        harness.Signal.Wake();
        harness.Clock.Advance(RetryHarness.FirstBackOff);

        var waited = Stopwatch.StartNew();
        while (harness.ProviderRequests.Count == 0 && waited.Elapsed < TimeSpan.FromSeconds(10))
        {
            // Nudges the clock in case the worker set its timer just after the advance above.
            await Task.Delay(10, TestContext.Current.CancellationToken);
            harness.Clock.Advance(Millisecond);
        }

        harness.ProviderRequests.ShouldHaveSingleItem().Headers.GetValues("Idempotency-Key").ShouldBe([payment.IdempotencyKey.ToString()]);
    }

    private static HttpResponseMessage Answer(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
