using System.Net;

namespace Lamina.Stubs.WireMock.Tests;

public sealed class RuleTests
{
    [Fact]
    public async Task ASpecsRuleMatchesOnlyRequestsCarryingItsAliasedValue()
    {
        await using var server = new ProviderServer();
        await using var run = await server.StartRunAsync();
        await using var declining = run.CreateScope(channel: null, specName: "declining");
        await using var accepting = run.CreateScope(channel: null, specName: "accepting");
        var decliningStub = declining.Services.GetRequiredService<PaymentProviderStub>();
        var acceptingStub = accepting.Services.GetRequiredService<PaymentProviderStub>();

        await decliningStub.WillDecline(decliningStub.Card("visa"));
        await acceptingStub.WillAccept(acceptingStub.Card("visa"), "pay_1");

        (await server.CreatePaymentAsync(decliningStub.Card("visa"))).ShouldBe(HttpStatusCode.PaymentRequired);
        (await server.CreatePaymentAsync(acceptingStub.Card("visa"))).ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ARuleWithoutTheTestIdIsRefusedBeforeItReachesWireMock()
    {
        await using var server = new ProviderServer();
        await using var run = await server.StartRunAsync();
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var stub = scope.Services.GetRequiredService<PaymentProviderStub>();
        var before = server.MappingIds;

        var refused = await Should.ThrowAsync<InvalidOperationException>(stub.WillDeclineAnyCard);

        refused.Message.ShouldContain("isn't keyed to this spec");
        server.MappingIds.ShouldBe(before);
    }

    [Fact]
    public async Task ASpecsRuleBeatsTheRunDefault()
    {
        await using var server = new ProviderServer();
        await using var run = await server.StartRunAsync();
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var stub = scope.Services.GetRequiredService<PaymentProviderStub>();

        await stub.WillDecline(stub.Card("visa"));
        await using (await server.StartRunAsync())
        {
            // A second run re-posts the default after the spec's rule, so only priority can decide.
        }

        (await server.CreatePaymentAsync(stub.Card("visa"))).ShouldBe(HttpStatusCode.PaymentRequired);
        (await server.CreatePaymentAsync(stub.Card("amex"))).ShouldBe(HttpStatusCode.Created);
    }
}
