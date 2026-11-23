using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Lamina.Stubs;
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
        await using var host = await StartPinnedWireMockAsync(ct);
        var stubUrl = host.GetEndpoint("payment-provider", "http");
        await using var run = await new SmokeSuite(stubUrl).StartAsync(ct);

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

    private static async Task<DistributedApplication> StartPinnedWireMockAsync(CancellationToken ct)
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Lamina_IntegrationTests_AppHost>(ct);
        var app = await builder.BuildAsync(ct);
        await app.StartAsync(ct);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("payment-provider", ct);
        return app;
    }

    private async Task<HttpStatusCode> CreatePaymentAsync(Uri stubUrl, string card, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync(new Uri(stubUrl, "payments"), new { amount = 999, cardToken = card }, ct);
        return response.StatusCode;
    }

    private sealed class SmokeSuite(Uri stubUrl) : LaminaSuite
    {
        protected override void Configure(LaminaBuilder builder) =>
            builder
                .UseSut(sut => sut.AddProvider("container", _ => new ContainerSut(stubUrl)))
                .AddChannel("api", isDefault: true)
                .AddStub<SmokeStub>();
    }

    private sealed class ContainerSut(Uri stubUrl) : ISystemUnderTest, ISutProvider
    {
        public IReadOnlyDictionary<string, string> Metadata { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public Uri Endpoint(string name) => stubUrl;

        public Task<ISystemUnderTest> StartAsync(CancellationToken ct) => Task.FromResult<ISystemUnderTest>(this);

        public Task WaitUntilReadyAsync(CancellationToken ct) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
