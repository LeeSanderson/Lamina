// PROTOTYPE: an in-memory fake of Folio Commerce, so the wiring can run without Aspire. Not part of the question.
// What IS real, for "Stub abstraction over WireMock" (issues/22):
//  - the payment provider is a real WireMock.Net server (in-process here; a container in the AppHost), endpoint
//    "payment-provider". Payments talks to it over HTTP exactly as the real service would.
//  - Payments' webhook route is a real Kestrel endpoint, "payments" + /webhooks/payment-provider.
//  - the retry after a lost answer runs on a background task with no baggage, like Folio's DB-backed worker in
//    phase 1 (no Lamina.Runtime). That's why stub rules key on the aliased card, not on baggage.
using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Lamina;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WireMock.Server;
using WireMock.Settings;

namespace Folio.Commerce.Acceptance.FakeSut;

public sealed record Book(string Id, string Title, decimal Price, int OnHand, int Reserved)
{
    public int Available => OnHand - Reserved;
}

public sealed record Order(string Id, string Customer, string BookId, string CardToken, string Status, string? Reason);

public sealed class InMemoryFolio : ISystemUnderTest
{
    private readonly ConcurrentDictionary<string, Book> books = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Order> orders = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> webhookBaggage = new();
    private readonly Lock gate = new();
    private WireMockServer? provider;
    private WebApplication? payments;
    private Uri? paymentsUrl;
    private HttpClient? providerClient;

    /// <summary>Baggage headers seen on webhooks, for the prototype's own check.</summary>
    public IEnumerable<string> WebhookBaggage => webhookBaggage;

    public Uri Endpoint(string name) => name switch
    {
        "payment-provider" => new Uri(provider!.Url! + "/"),
        "payments" => paymentsUrl!,
        _ => new Uri($"memory://{name}"),
    };

    public async Task WaitUntilReadyAsync(CancellationToken ct)
    {
        provider = WireMockServer.Start(new WireMockServerSettings { StartAdminInterface = true });

        // Payments, as an external system sees it: one webhook route.
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        payments = builder.Build();
        payments.MapPost("/webhooks/payment-provider", (HttpRequest request, JsonObject body) =>
        {
            webhookBaggage.Enqueue(request.Headers["baggage"].ToString());
            if (body["type"]?.GetValue<string>() == "payment.confirmed") Confirm(body["reference"]!.GetValue<string>());
            return Results.Ok();
        });
        await payments.StartAsync(ct);
        paymentsUrl = new Uri(payments.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single() + "/");

        // Real Payments: no resilience handler on the provider client, 5 s timeout ("Folio phase 1 tech shape").
        providerClient = new HttpClient { BaseAddress = Endpoint("payment-provider"), Timeout = TimeSpan.FromSeconds(5) };
    }

    public async ValueTask DisposeAsync()
    {
        // PROTOTYPE check of per-spec cleanup: after the run only the run-level default should be left.
        if (provider is not null)
        {
            var left = provider.MappingModels.Select(m => m.Title).ToList();
            var report = $"mappings left: {left.Count} [{string.Join(", ", left)}]; journal entries: {provider.LogEntries.Count()}; webhooks with baggage: {webhookBaggage.Count(b => b.StartsWith("lamina.test=", StringComparison.Ordinal))}/{webhookBaggage.Count}"
                + "; responses: " + string.Join(", ", provider.LogEntries.GroupBy(e => $"{e.ResponseMessage?.StatusCode}/{e.ResponseMessage?.FaultType}").Select(g => $"{g.Key} x{g.Count()}"));
            await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "stub-report.txt"), report);
        }

        providerClient?.Dispose();
        if (payments is not null) await payments.DisposeAsync();
        provider?.Stop();
        provider?.Dispose();
    }

    public string AddBook(string title, int stock, decimal price)
    {
        var id = Guid.NewGuid().ToString("N");
        books[id] = new Book(id, title, price, stock, 0);
        return id;
    }

    public Book GetBook(string id) => books[id];

    public Order GetOrder(string id) => orders[id];

    public async Task<Order> PlaceOrder(string customer, string bookId, string cardToken)
    {
        var id = Guid.NewGuid().ToString("N");
        lock (gate) books[bookId] = books[bookId] with { Reserved = books[bookId].Reserved + 1 };
        orders[id] = new Order(id, customer, bookId, cardToken, "Pending", null);

        // Payments records the payment first; its ID is the Idempotency-Key for every attempt.
        var idempotencyKey = Guid.NewGuid().ToString("N");
        var price = books[bookId].Price;
        var outcome = await CreatePayment(id, cardToken, price, idempotencyKey);
        if (outcome == Outcome.Declined)
        {
            Release(bookId);
            return orders[id] = orders[id] with { Status = "Rejected", Reason = "PaymentDeclined" };
        }

        if (outcome == Outcome.Unknown) _ = RetryInWorker(id, cardToken, price, idempotencyKey);
        return orders[id];
    }

    private enum Outcome { Pending, Declined, Unknown }

    private async Task<Outcome> CreatePayment(string reference, string cardToken, decimal price, string idempotencyKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "payments")
        {
            Content = JsonContent.Create(new { amount = (long)(price * 100), currency = "GBP", cardToken, reference }),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        try
        {
            using var response = await providerClient!.SendAsync(request);
            switch ((int)response.StatusCode)
            {
                case 201:
                    // As the real Payments: the answer counts only if it can be read. An empty or garbled body means
                    // charged-or-not is unknown, so the payment stays Pending and the worker retries with the same key.
                    var body = await response.Content.ReadFromJsonAsync<JsonObject>();
                    return body?["id"] is null ? Outcome.Unknown : Outcome.Pending;
                case 402:
                    return Outcome.Declined;
                default:
                    return Outcome.Unknown;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            return Outcome.Unknown;
        }
        catch (HttpRequestException)
        {
            // Dropped connection: charged or not, we don't know. Leave it Pending and retry.
            return Outcome.Unknown;
        }
        catch (TaskCanceledException)
        {
            return Outcome.Unknown;
        }
    }

    // Stands in for the DB-backed worker: a fresh execution context, no baggage, fast retries, same key.
    private Task RetryInWorker(string reference, string cardToken, decimal price, string idempotencyKey) =>
        Task.Run(async () =>
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                await Task.Delay(50);
                var outcome = await CreatePayment(reference, cardToken, price, idempotencyKey);
                if (outcome == Outcome.Pending) return;
                if (outcome == Outcome.Declined)
                {
                    var order = orders[reference];
                    Release(order.BookId);
                    orders[reference] = order with { Status = "Rejected", Reason = "PaymentDeclined" };
                    return;
                }
            }
        });

    private void Confirm(string reference)
    {
        if (!orders.TryGetValue(reference, out var order) || order.Status != "Pending") return;
        orders[reference] = order with { Status = "Confirmed" };
        lock (gate)
        {
            var book = books[order.BookId];
            books[order.BookId] = book with { OnHand = book.OnHand - 1, Reserved = book.Reserved - 1 };
        }
    }

    private void Release(string bookId)
    {
        lock (gate) books[bookId] = books[bookId] with { Reserved = books[bookId].Reserved - 1 };
    }
}
