using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using WireMock.Server;
using WireMock.Settings;

namespace Lamina.Stubs.WireMock.Tests;

// An in-process WireMock.Net server, and the test's own view of it: what the SUT would send, and what mappings exist.
public sealed class ProviderServer : IAsyncDisposable
{
    // Bound to loopback only. WireMock's default listens on every interface (http://*:port),
    // which makes the Windows firewall prompt for each new test executable path.
    private readonly WireMockServer _server = WireMockServer.Start(new WireMockServerSettings
    {
        Urls = [$"http://127.0.0.1:{FreeLoopbackPort()}"],
        StartAdminInterface = true,
    });

    private readonly HttpClient _http = new();

    public Uri Url => new(_server.Url!);

    public IReadOnlyList<Guid> MappingIds => [.. _server.Mappings.Where(m => !m.IsAdminInterface).Select(m => m.Guid)];

    public int JournalCount => _server.LogEntries.Count;

    public Task<LaminaRun> StartRunAsync() =>
        new WireMockSuite(Url).StartAsync(TestContext.Current.CancellationToken);

    public IReadOnlyList<string> ScenarioNames => [.. _server.Scenarios.Select(s => s.Name)];

    public async Task<HttpStatusCode> CreatePaymentAsync(string card) =>
        (await CreatePaymentAnsweredAsync(card)).Status;

    public async Task<(HttpStatusCode Status, string Body)> CreatePaymentAnsweredAsync(string card)
    {
        using var response = await _http.PostAsJsonAsync(
            new Uri(Url, "payments"),
            new { amount = 999, currency = "GBP", cardToken = card, reference = $"order-{card}" },
            TestContext.Current.CancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    public async Task<HttpStatusCode> CreatePaymentWithinAsync(string card, TimeSpan timeout)
    {
        using var http = new HttpClient { Timeout = timeout };
        using var response = await http.PostAsJsonAsync(
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

    private static int FreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return listener.LocalEndpoint is IPEndPoint endpoint
                ? endpoint.Port
                : throw new InvalidOperationException("The loopback listener has no IP endpoint.");
        }
        finally
        {
            listener.Stop();
        }
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
