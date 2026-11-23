using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Lamina.Stubs;
using Lamina.Sut.Aspire;
using Microsoft.Extensions.DependencyInjection;

namespace Lamina.IntegrationTests;

// Checks Lamina's WireMock admin client against the pinned image, which lags the WireMock.Net NuGet release.
public sealed class WireMockImageSmokeTests : IDisposable
{
    private readonly HttpClient _http = new();

    [Fact]
    public async Task TheAdminClientDrivesThePinnedImage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var run = await new SmokeSuite().StartAsync(ct);
        var stubUrl = run.Sut.Endpoint("payment-provider");

        string card;
        await using (var scope = run.CreateScope(channel: null, specName: "smoke"))
        {
            var stub = scope.Services.GetRequiredService<SmokeStub>();
            card = stub.Card("visa");
            await stub.WillDecline(card);

            (await CreatePaymentAsync(stubUrl, card, ct)).ShouldBe(HttpStatusCode.PaymentRequired);
            (await CreatePaymentAsync(stubUrl, "someone-else", ct)).ShouldBe(HttpStatusCode.Created);
            var journalled = (await stub.Creates(card)).ShouldHaveSingleItem();
            journalled.Status.ShouldBe(402);
            journalled.Body!["cardToken"]!.GetValue<string>().ShouldBe(card);
        }

        (await CreatePaymentAsync(stubUrl, card, ct)).ShouldBe(HttpStatusCode.Created);
        var mappings = JsonNode.Parse(await _http.GetStringAsync(new Uri(stubUrl, "__admin/mappings"), ct))!.AsArray();
        mappings.Select(m => m!["Guid"]!.GetValue<string>()).ShouldBe([SmokeStub.AcceptAnyCardId.ToString()]);
    }

    public void Dispose() => _http.Dispose();

    private async Task<HttpStatusCode> CreatePaymentAsync(Uri stubUrl, string card, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync(new Uri(stubUrl, "payments"), new { amount = 999, cardToken = card }, ct);
        return response.StatusCode;
    }

    private sealed class SmokeSuite : LaminaSuite
    {
        protected override void Configure(LaminaBuilder builder) =>
            builder
                .UseSut(sut => sut.Aspire<Projects.Lamina_IntegrationTests_AppHost>())
                .AddChannel("api", isDefault: true)
                .AddStub<SmokeStub>();
    }
}
