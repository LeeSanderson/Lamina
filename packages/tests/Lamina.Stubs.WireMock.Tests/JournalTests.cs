namespace Lamina.Stubs.WireMock.Tests;

public sealed class JournalTests
{
    [Fact]
    public async Task TheJournalReturnsOnlyThisSpecsRequestsOldestFirst()
    {
        await using var server = new ProviderServer();
        await using var run = await server.StartRunAsync();
        await using var mine = run.CreateScope(channel: null, specName: "mine");
        await using var theirs = run.CreateScope(channel: null, specName: "theirs");
        var myStub = mine.Services.GetRequiredService<PaymentProviderStub>();
        var theirStub = theirs.Services.GetRequiredService<PaymentProviderStub>();
        await myStub.WillDecline(myStub.Card("visa"));

        await server.CreatePaymentAsync(myStub.Card("visa"));
        await server.CreatePaymentAsync(theirStub.Card("visa"));
        await server.CreatePaymentAsync(myStub.Card("visa"));

        var creates = await myStub.Creates(myStub.Card("visa"));

        creates.Select(c => c.Body!["cardToken"]!.GetValue<string>()).ShouldBe([myStub.Card("visa"), myStub.Card("visa")]);
        creates.ShouldAllBe(c => c.Status == 402 && c.Method == "POST" && c.Path == "/payments");
        creates[0].ReceivedAt.ShouldBeLessThanOrEqualTo(creates[1].ReceivedAt);
        creates[0].ResponseBody!["error"]!.GetValue<string>().ShouldBe("card_declined");
        creates[0].Header("content-type").ShouldStartWith("application/json");
    }

    [Fact]
    public async Task TheJournalShowsWhatTheRunDefaultAnswered()
    {
        await using var server = new ProviderServer();
        await using var run = await server.StartRunAsync();
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var stub = scope.Services.GetRequiredService<PaymentProviderStub>();

        await server.CreatePaymentAsync(stub.Card("visa"));

        var create = (await stub.Creates(stub.Card("visa"))).ShouldHaveSingleItem();
        create.Status.ShouldBe(201);
        create.ResponseBody!["id"]!.GetValue<string>().ShouldBe("pay_default");
    }

    [Fact]
    public async Task WaitingOnTheJournalForACreateThatNeverCameFailsThroughEventually()
    {
        await using var server = new ProviderServer();
        await using var run = await server.StartRunAsync();
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var stub = scope.Services.GetRequiredService<PaymentProviderStub>();

        var failed = await Should.ThrowAsync<EventuallyFailedException>(
            () => stub.AcceptedCreate(stub.Card("visa"), TimeSpan.FromMilliseconds(200)));

        failed.Message.ShouldStartWith("Gave up waiting: the provider should have accepted a payment created with card ");
        failed.Message.ShouldContain("Last observed (attempt ");
        failed.Message.ShouldContain("[]");
    }

    [Fact]
    public async Task AJournalQueryWithoutTheTestIdIsRefused()
    {
        await using var server = new ProviderServer();
        await using var run = await server.StartRunAsync();
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var stub = scope.Services.GetRequiredService<PaymentProviderStub>();

        var refused = await Should.ThrowAsync<InvalidOperationException>(stub.AnyCreates);

        refused.Message.ShouldContain("journal query POST /payments isn't keyed to this spec");
    }
}
