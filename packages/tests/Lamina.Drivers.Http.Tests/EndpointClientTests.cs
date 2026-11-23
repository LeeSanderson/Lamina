using System.Net.Http.Json;

namespace Lamina.Drivers.Http.Tests;

public sealed class EndpointClientTests
{
    [Fact]
    public async Task ANamedClientSendsToTheUriTheSutReturnsForItsEndpoint()
    {
        await using var run = await HttpSuite.StartAsync(lamina => lamina.AddHttpClient("web-bff"));
        await using var scope = run.CreateScope(channel: null, specName: "spec");

        var client = scope.Services.GetRequiredService<SutHttpClients>().For("web-bff");
        var echo = await client.GetFromJsonAsync<Echo>("api/books", TestContext.Current.CancellationToken);

        echo.ShouldNotBeNull().Path.ShouldBe("/web-bff/api/books");
    }

    [Fact]
    public async Task ATypedClientSendsToTheUriTheSutReturnsForItsEndpoint()
    {
        await using var run = await HttpSuite.StartAsync(lamina => lamina.AddHttpClient<PaymentsApi>("payments"));
        await using var scope = run.CreateScope(channel: null, specName: "spec");

        var echo = await scope.Services.GetRequiredService<PaymentsApi>().GetAsync("webhooks/payment-provider");

        echo.Path.ShouldBe("/payments/webhooks/payment-provider");
    }

    [Fact]
    public async Task AskingForAnUnregisteredEndpointNamesTheRegisteredOnes()
    {
        await using var run = await HttpSuite.StartAsync(lamina => lamina.AddHttpClient("web-bff"));
        await using var scope = run.CreateScope(channel: null, specName: "spec");

        var clients = scope.Services.GetRequiredService<SutHttpClients>();

        Should.Throw<InvalidOperationException>(() => clients.For("payments")).Message.ShouldContain("Registered: web-bff.");
    }

    [Fact]
    public async Task AClientForAnEndpointTheSutDoesNotHaveFailsTheRunAtStart()
    {
        var error = await Should.ThrowAsync<InvalidOperationException>(() => HttpSuite.StartAsync(
            lamina =>
            {
                lamina.AddHttpClient("web-bff");
                lamina.AddHttpClient<PaymentsApi>("payments");
            },
            endpoints: ["web-bff"]));

        error.Message.ShouldBe("The suite needs SUT endpoints that SUT provider 'fake' doesn't have: payments. It has: web-bff.");
    }

    public sealed class PaymentsApi(HttpClient http)
    {
        public async Task<Echo> GetAsync(string path) =>
            (await http.GetFromJsonAsync<Echo>(path, TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }
}
