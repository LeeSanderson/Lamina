using System.Diagnostics;
using System.Net;

namespace Lamina.Stubs.WireMock.Tests;

public sealed class FaultTests
{
    [Fact]
    public async Task WillFailAnswersTheStatusNTimesThenFallsThroughToTheRunDefault()
    {
        await using var server = new ProviderServer();
        await using var run = await server.StartRunAsync();
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var stub = scope.Services.GetRequiredService<PaymentProviderStub>();
        var card = stub.Card("visa");

        await stub.WillFail(card, 503, times: 2);

        (await server.CreatePaymentAsync(card)).ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await server.CreatePaymentAsync(card)).ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await server.CreatePaymentAsync(card)).ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AFaultBeatsTheSpecsOwnRuleAndThenFallsThroughToIt()
    {
        await using var server = new ProviderServer();
        await using var run = await server.StartRunAsync();
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var stub = scope.Services.GetRequiredService<PaymentProviderStub>();
        var card = stub.Card("visa");

        // The spec's rule goes in after the fault, so only priority can put the fault first.
        await stub.WillFail(card, 503, times: 1);
        await stub.WillDecline(card);

        (await server.CreatePaymentAsync(card)).ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await server.CreatePaymentAsync(card)).ShouldBe(HttpStatusCode.PaymentRequired);
    }

    [Fact]
    public async Task WillAnswerEmptyAnswersTheStatusWithNoBodyAndTheJournalSeesTheRequestAtOnce()
    {
        await using var server = new ProviderServer();
        await using var run = await server.StartRunAsync();
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var stub = scope.Services.GetRequiredService<PaymentProviderStub>();
        var card = stub.Card("visa");

        await stub.LosesAnswer(card, times: 1);

        (await server.CreatePaymentAnsweredAsync(card)).ShouldBe((HttpStatusCode.Created, string.Empty));
        var create = (await stub.Creates(card)).ShouldHaveSingleItem();
        create.Status.ShouldBe(201);
        create.AnswerLost.ShouldBeTrue();
        var (status, body) = await server.CreatePaymentAnsweredAsync(card);
        status.ShouldBe(HttpStatusCode.Created);
        body.ShouldContain("pay_default");
        (await stub.Creates(card))[^1].AnswerLost.ShouldBeFalse();
    }

    [Fact]
    public async Task WillAnswerMalformedAnswersTheStatusWithAGarbageBody()
    {
        await using var server = new ProviderServer();
        await using var run = await server.StartRunAsync();
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var stub = scope.Services.GetRequiredService<PaymentProviderStub>();
        var card = stub.Card("visa");

        await stub.AnswersGarbage(card, times: 1);

        var (status, body) = await server.CreatePaymentAnsweredAsync(card);
        status.ShouldBe(HttpStatusCode.Created);
        body.ShouldNotBeEmpty();
        body.ShouldNotContain("pay_default");
        (await stub.Creates(card)).ShouldHaveSingleItem().AnswerLost.ShouldBeTrue();
    }

    [Fact]
    public async Task WillTimeOutHoldsTheAnswerPastTheCallersTimeoutAndTheJournalDoesNotSeeTheRequestMeanwhile()
    {
        // Stopping the server waits for the held request, so it stops in the background instead of holding up the run.
        var server = new ProviderServer();
        try
        {
            await using var run = await server.StartRunAsync();
            await using var scope = run.CreateScope(channel: null, specName: "spec");
            var stub = scope.Services.GetRequiredService<PaymentProviderStub>();
            var card = stub.Card("visa");

            await stub.NeverAnswers(card);

            await Should.ThrowAsync<TaskCanceledException>(() => server.CreatePaymentWithinAsync(card, TimeSpan.FromMilliseconds(500)));
            (await stub.Creates(card)).ShouldBeEmpty();
        }
        finally
        {
            _ = Task.Run(async () => await server.DisposeAsync(), CancellationToken.None);
        }
    }

    [Fact]
    public async Task WillDelayHoldsTheAnswerThenAnswersAndTheJournalSeesTheRequestOnlyWhenTheHoldEnds()
    {
        await using var server = new ProviderServer();
        await using var run = await server.StartRunAsync();
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var stub = scope.Services.GetRequiredService<PaymentProviderStub>();
        var card = stub.Card("visa");
        var delay = TimeSpan.FromMilliseconds(1500);

        await stub.AcceptsSlowly(card, delay);

        var clock = Stopwatch.StartNew();
        var answer = server.CreatePaymentAnsweredAsync(card);
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
        (await stub.Creates(card)).ShouldBeEmpty();
        var (status, body) = await answer;
        clock.Elapsed.ShouldBeGreaterThanOrEqualTo(delay - TimeSpan.FromMilliseconds(50));
        status.ShouldBe(HttpStatusCode.Created);
        body.ShouldContain("pay_slow");
        (await stub.Creates(card)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task FaultsAndTheirScenariosAreDeletedWhenTheSpecEnds()
    {
        await using var server = new ProviderServer();
        await using var run = await server.StartRunAsync();
        string card;
        await using (var scope = run.CreateScope(channel: null, specName: "spec"))
        {
            var stub = scope.Services.GetRequiredService<PaymentProviderStub>();
            card = stub.Card("visa");
            await stub.LosesAnswer(card, times: 3);
            await stub.WillFail(stub.Card("amex"), 503, times: 1);
            await stub.NeverAnswers(stub.Card("diners"));
            server.ScenarioNames.Count.ShouldBe(2);
        }

        server.MappingIds.ShouldBe([PaymentProviderStub.AcceptAnyCardId]);
        server.ScenarioNames.ShouldBeEmpty();
        (await server.CreatePaymentAnsweredAsync(card)).Body.ShouldContain("pay_default");
    }

    [Fact]
    public async Task AFaultWithoutTheTestIdIsRefusedBeforeItReachesWireMock()
    {
        await using var server = new ProviderServer();
        await using var run = await server.StartRunAsync();
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var stub = scope.Services.GetRequiredService<PaymentProviderStub>();
        var before = server.MappingIds;

        var refused = await Should.ThrowAsync<InvalidOperationException>(() => stub.LosesAnyAnswer(times: 1));

        refused.Message.ShouldContain("isn't keyed to this spec");
        server.MappingIds.ShouldBe(before);
    }

    [Fact]
    public async Task ALostAnswerThenSuccessScenarioIsPerSpecEvenWhenSpecsShareACardName()
    {
        await using var server = new ProviderServer();
        await using var run = await server.StartRunAsync();
        await using var losing = run.CreateScope(channel: null, specName: "losing");
        await using var other = run.CreateScope(channel: null, specName: "other");
        var losingStub = losing.Services.GetRequiredService<PaymentProviderStub>();
        var otherStub = other.Services.GetRequiredService<PaymentProviderStub>();
        await losingStub.LosesAnswer(losingStub.Card("visa"), times: 1);
        await otherStub.LosesAnswer(otherStub.Card("visa"), times: 2);

        (await server.CreatePaymentAnsweredAsync(otherStub.Card("visa"))).Body.ShouldBeEmpty();
        (await server.CreatePaymentAnsweredAsync(losingStub.Card("visa"))).Body.ShouldBeEmpty();
        (await server.CreatePaymentAnsweredAsync(losingStub.Card("visa"))).Body.ShouldContain("pay_default");
        (await server.CreatePaymentAnsweredAsync(otherStub.Card("visa"))).Body.ShouldBeEmpty();
        (await server.CreatePaymentAnsweredAsync(otherStub.Card("visa"))).Body.ShouldContain("pay_default");
    }
}
