using System.Net;
using System.Net.Http.Json;
using WireMock.Server;

namespace Lamina.Stubs.WireMock.Tests;

// An in-process WireMock.Net server, and the test's own view of it: what the SUT would send, and what mappings exist.
public sealed class ProviderServer : IAsyncDisposable
{
    private readonly WireMockServer _server = WireMockServer.StartWithAdminInterface();
    private readonly HttpClient _http = new();

    public Uri Url => new(_server.Url!);

    public IReadOnlyList<Guid> MappingIds => [.. _server.Mappings.Where(m => !m.IsAdminInterface).Select(m => m.Guid)];

    public int JournalCount => _server.LogEntries.Count;

    public Task<LaminaRun> StartRunAsync() =>
        new WireMockSuite(Url).StartAsync(TestContext.Current.CancellationToken);

    public async Task<HttpStatusCode> CreatePaymentAsync(string card)
    {
        using var response = await _http.PostAsJsonAsync(
            new Uri(Url, "payments"),
            new { amount = 999, currency = "GBP", cardToken = card, reference = $"order-{card}" },
            TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    public ValueTask DisposeAsync()
    {
        _http.Dispose();
        _server.Stop();
        _server.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class WireMockSuite(Uri provider) : LaminaSuite
    {
        protected override void Configure(LaminaBuilder builder) =>
            builder
                .UseSut(sut => sut.AddProvider("stubbed", _ => new StubbedSut(provider)))
                .AddChannel("api", isDefault: true)
                .AddStub<PaymentProviderStub>();
    }
}
