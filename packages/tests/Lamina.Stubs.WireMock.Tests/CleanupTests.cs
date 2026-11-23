using System.Net;

namespace Lamina.Stubs.WireMock.Tests;

public sealed class CleanupTests
{
    [Fact]
    public async Task WhenASpecEndsItsRulesAreDeletedAndTheDefaultStays()
    {
        await using var server = new ProviderServer();
        await using var run = await server.StartRunAsync();
        string card;
        await using (var scope = run.CreateScope(channel: null, specName: "spec"))
        {
            var stub = scope.Services.GetRequiredService<PaymentProviderStub>();
            card = stub.Card("visa");
            await stub.WillDecline(card);
            await stub.WillDecline(stub.Card("amex"));
            server.MappingIds.Count.ShouldBe(3);
        }

        server.MappingIds.ShouldBe([PaymentProviderStub.AcceptAnyCardId]);
        (await server.CreatePaymentAsync(card)).ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ASpecThatCreatedNoRulesLeavesEveryOtherRuleInPlace()
    {
        await using var server = new ProviderServer();
        await using var run = await server.StartRunAsync();
        await using var busy = run.CreateScope(channel: null, specName: "busy");
        var busyStub = busy.Services.GetRequiredService<PaymentProviderStub>();
        await busyStub.WillDecline(busyStub.Card("visa"));
        var before = server.MappingIds;

        await using (var idle = run.CreateScope(channel: null, specName: "idle"))
        {
            var idleStub = idle.Services.GetRequiredService<PaymentProviderStub>();
            await idleStub.Creates(idleStub.Card("visa"));
        }

        server.MappingIds.ShouldBe(before, ignoreOrder: true);
    }

    [Fact]
    public async Task TheJournalIsNotCleanedWhenASpecEnds()
    {
        await using var server = new ProviderServer();
        await using var run = await server.StartRunAsync();
        string card;
        await using (var first = run.CreateScope(channel: null, specName: "first"))
        {
            var stub = first.Services.GetRequiredService<PaymentProviderStub>();
            card = stub.Card("visa");
            await stub.WillDecline(card);
            await server.CreatePaymentAsync(card);
        }

        server.JournalCount.ShouldBe(1);
    }
}
