using System.Net;

namespace Lamina.Stubs.WireMock.Tests;

public sealed class RunDefaultsTests
{
    [Fact]
    public async Task DefaultsAreInstalledAtRunStartUnderTheirFixedIds()
    {
        await using var server = new ProviderServer();

        await using var run = await server.StartRunAsync();

        server.MappingIds.ShouldBe([PaymentProviderStub.AcceptAnyCardId]);
        (await server.CreatePaymentAsync("any-card")).ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task InstallingDefaultsAgainReplacesThemRatherThanAddingCopies()
    {
        await using var server = new ProviderServer();

        await using (await server.StartRunAsync())
        {
            // First run against a long-running SUT.
        }

        await using var second = await server.StartRunAsync();

        server.MappingIds.ShouldBe([PaymentProviderStub.AcceptAnyCardId]);
    }

    [Fact]
    public async Task ADefaultInsideASpecIsRefused()
    {
        await using var server = new ProviderServer();
        await using var run = await server.StartRunAsync();
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var stub = scope.Services.GetRequiredService<PaymentProviderStub>();

        var refused = await Should.ThrowAsync<InvalidOperationException>(stub.AcceptAnyCard);

        refused.Message.ShouldContain("InstallDefaults()");
    }
}
