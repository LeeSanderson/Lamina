using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace Folio.Payments.Tests;

// Payments' retry worker in a host of its own: an in-memory database, a fake clock, and fake provider and Ordering
// answers. The provider's answer to each resubmission comes from the test.
internal sealed class RetryHarness : IAsyncDisposable
{
    internal static readonly TimeSpan FirstBackOff = TimeSpan.FromMilliseconds(50);
    internal const int Retries = 5;

    private readonly IHost _host;
    private readonly RecordingHandler _provider;
    private readonly RecordingHandler _ordering;

    private RetryHarness(FakeTimeProvider clock, IHost host, RecordingHandler provider, RecordingHandler ordering)
    {
        Clock = clock;
        _host = host;
        _provider = provider;
        _ordering = ordering;
    }

    internal FakeTimeProvider Clock { get; }

    internal PaymentRetryWorker Worker => _host.Services.GetServices<IHostedService>().OfType<PaymentRetryWorker>().Single();

    internal PaymentRetrySignal Signal => _host.Services.GetRequiredService<PaymentRetrySignal>();

    // The fake clock's time when each create reached the provider.
    internal IReadOnlyList<DateTimeOffset> ProviderCallTimes => _provider.Times;

    internal IReadOnlyList<HttpRequestMessage> ProviderRequests => _provider.Requests;

    internal IReadOnlyList<(string Path, string Body)> OrderingCalls => _ordering.Calls;

    internal static RetryHarness ProviderAnswering(Func<HttpResponseMessage> answer) =>
        ProviderAnswering(_ => Task.FromResult(answer()));

    // The answer may act on the harness first, as something else touching the payment while the call is in flight.
    internal static RetryHarness ProviderAnswering(Func<RetryHarness, Task<HttpResponseMessage>> answer)
    {
        RetryHarness? harness = null;
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero));
        var provider = new RecordingHandler(clock.GetUtcNow, () => answer(harness!));
        var ordering = new RecordingHandler(clock.GetUtcNow, () => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)));
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["Clients:PaymentProvider"] = "http://payment-provider";
        builder.Configuration["Clients:Ordering"] = "http://ordering";
        builder.Configuration["PaymentProvider:Retry:FirstBackOff"] = FirstBackOff.ToString("c", CultureInfo.InvariantCulture);
        builder.Configuration["PaymentProvider:Retry:Attempts"] = Retries.ToString(CultureInfo.InvariantCulture);
        builder.Services.AddSingleton<TimeProvider>(clock);
        var database = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<PaymentsDbContext>(options => options.UseInMemoryDatabase(database));
        builder.AddPaymentProviderClient();
        builder.Services.AddHttpClient<PaymentProviderClient>().ConfigurePrimaryHttpMessageHandler(() => provider);
        builder.AddOrderingClient();
        builder.Services.AddHttpClient<OrderingClient>().ConfigurePrimaryHttpMessageHandler(() => ordering);
        builder.AddPaymentRetries();
        var host = builder.Build();
        harness = new RetryHarness(clock, host, provider, ordering);
        return harness;
    }

    internal Task StartAsync() => _host.StartAsync(TestContext.Current.CancellationToken);

    // A payment whose first create lost its answer just now, as Payments records it.
    internal async Task<Payment> ALostPaymentAsync()
    {
        var now = Clock.GetUtcNow();
        var id = Guid.CreateVersion7();
        var payment = new Payment
        {
            Id = id,
            OrderId = Guid.NewGuid(),
            Reference = $"folio-{id:N}",
            IdempotencyKey = Guid.NewGuid(),
            Amount = 25.99m,
            CardToken = "visa-k3f9",
            Status = PaymentStatus.AwaitingSubmission,
            CreatedAt = now,
            Attempts = 1,
            NextAttemptAt = now + FirstBackOff,
        };
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        db.Payments.Add(payment);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return payment;
    }

    // What the provider's payment.succeeded webhook does to the payment, in a scope of its own.
    internal async Task ConfirmAsTheWebhookDoesAsync(Guid id, string providerPaymentId)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var payment = await db.Payments.SingleAsync(p => p.Id == id, TestContext.Current.CancellationToken);
        payment.Confirm(providerPaymentId).ShouldBeTrue();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    internal async Task<Payment> ReadAsync(Guid id)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().Payments.AsNoTracking()
            .SingleAsync(p => p.Id == id, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync(CancellationToken.None);
        _host.Dispose();
    }

    private sealed class RecordingHandler(Func<DateTimeOffset> now, Func<Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        private readonly ConcurrentQueue<HttpRequestMessage> _requests = new();
        private readonly ConcurrentQueue<DateTimeOffset> _times = new();
        private readonly ConcurrentQueue<(string Path, string Body)> _calls = new();

        internal IReadOnlyList<HttpRequestMessage> Requests => [.. _requests];

        internal IReadOnlyList<DateTimeOffset> Times => [.. _times];

        internal IReadOnlyList<(string Path, string Body)> Calls => [.. _calls];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            _times.Enqueue(now());
            _calls.Enqueue((request.RequestUri!.AbsolutePath, body));
            _requests.Enqueue(request);
            return await answer();
        }
    }
}
