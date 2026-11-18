// PROTOTYPE: an in-memory fake of Folio Commerce plus the payment provider, so the wiring can run without Aspire.
// It is not part of the question; the real drivers talk HTTP to the Web BFF, and the stub is WireMock.
using System.Collections.Concurrent;
using Lamina;

namespace Folio.Commerce.Acceptance.FakeSut;

public enum ProviderRule { Accept, Decline, DropOnce }

public sealed record Book(string Id, string Title, decimal Price, int OnHand, int Reserved)
{
    public int Available => OnHand - Reserved;
}

public sealed record Order(string Id, string Customer, string BookId, string CardToken, string Status, string? Reason);

public sealed record ProviderCreate(string CardToken, string IdempotencyKey);

public sealed class InMemoryFolio : ISystemUnderTest
{
    private readonly ConcurrentDictionary<string, Book> books = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Order> orders = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ProviderRule> providerRules = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<ProviderCreate> providerJournal = new();
    private readonly Lock gate = new();

    public Uri Endpoint(string name) => new($"memory://{name}");

    public Task WaitUntilReadyAsync(CancellationToken ct) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public string AddBook(string title, int stock, decimal price)
    {
        var id = Guid.NewGuid().ToString("N");
        books[id] = new Book(id, title, price, stock, 0);
        return id;
    }

    public Book GetBook(string id) => books[id];

    public Order GetOrder(string id) => orders[id];

    public Order PlaceOrder(string customer, string bookId, string cardToken)
    {
        var id = Guid.NewGuid().ToString("N");
        lock (gate) books[bookId] = books[bookId] with { Reserved = books[bookId].Reserved + 1 };

        providerJournal.Enqueue(new ProviderCreate(cardToken, id));
        var rule = providerRules.GetValueOrDefault(cardToken, ProviderRule.Accept);
        if (rule == ProviderRule.Decline)
        {
            Release(bookId);
            return orders[id] = new Order(id, customer, bookId, cardToken, "Rejected", "PaymentDeclined");
        }

        if (rule == ProviderRule.DropOnce)
        {
            providerRules[cardToken] = ProviderRule.Accept;
            _ = RetryLaterAsync(cardToken, id);
        }

        return orders[id] = new Order(id, customer, bookId, cardToken, "Pending", null);
    }

    public void SetProviderRule(string cardToken, ProviderRule rule) => providerRules[cardToken] = rule;

    public IReadOnlyList<ProviderCreate> ProviderJournal(string cardToken) => providerJournal.Where(c => c.CardToken == cardToken).ToList();

    public void ConfirmPaymentWebhook(string cardToken)
    {
        foreach (var order in orders.Values.Where(o => o.CardToken == cardToken && o.Status == "Pending"))
        {
            orders[order.Id] = order with { Status = "Confirmed" };
            lock (gate)
            {
                var book = books[order.BookId];
                books[order.BookId] = book with { OnHand = book.OnHand - 1, Reserved = book.Reserved - 1 };
            }
        }
    }

    private async Task RetryLaterAsync(string cardToken, string idempotencyKey)
    {
        await Task.Delay(50);
        providerJournal.Enqueue(new ProviderCreate(cardToken, idempotencyKey));
    }

    private void Release(string bookId)
    {
        lock (gate) books[bookId] = books[bookId] with { Reserved = books[bookId].Reserved - 1 };
    }
}
