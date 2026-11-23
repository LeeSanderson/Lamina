using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;

namespace Folio.Payments;

/// <summary>
/// Resubmits payments whose outcome is unknown, with the same Idempotency-Key, so the provider never charges twice.
/// It sleeps until the next payment is due, on <see cref="TimeProvider"/>, or until Payments wakes it after a lost answer.
/// </summary>
internal sealed partial class PaymentRetryWorker(
    IServiceScopeFactory scopes,
    PaymentRetrySignal signal,
    PaymentRetryPolicy policy,
    TimeProvider time,
    ILogger<PaymentRetryWorker> logger) : BackgroundService
{
    /// <summary>
    /// Resubmits every payment that is due now, and returns when the next one falls due, or <see langword="null"/>
    /// when none is waiting.
    /// </summary>
    internal async Task<DateTimeOffset?> RetryDueAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var provider = scope.ServiceProvider.GetRequiredService<PaymentProviderClient>();
        var ordering = scope.ServiceProvider.GetRequiredService<OrderingClient>();

        var now = time.GetUtcNow();
        var due = await db.Payments
            .Where(p => p.Status == PaymentStatus.AwaitingSubmission && p.NextAttemptAt <= now)
            .OrderBy(p => p.NextAttemptAt)
            .ToListAsync(cancellationToken);
        foreach (var payment in due)
        {
            await ResubmitAsync(payment, db, provider, ordering, cancellationToken);
        }

        return await db.Payments
            .Where(p => p.Status == PaymentStatus.AwaitingSubmission)
            .MinAsync(p => p.NextAttemptAt, cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            DateTimeOffset? next;
            try
            {
                next = await RetryDueAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                LogRetryPassFailed(exception);
                next = time.GetUtcNow() + policy.FirstBackOff;
            }

            await signal.WaitAsync(next, time, stoppingToken);
        }
    }

    private async Task ResubmitAsync(
        Payment payment,
        PaymentsDbContext db,
        PaymentProviderClient provider,
        OrderingClient ordering,
        CancellationToken cancellationToken)
    {
        // Counted and leased before the call, so a crash mid-call still counts the attempt and retries after the lease.
        payment.StartAttempt(time.GetUtcNow(), policy.SubmissionLease);
        await db.SaveChangesAsync(cancellationToken);

        var reply = await provider.CreatePaymentAsync(
            new PaymentCreate(payment.Reference, payment.IdempotencyKey, payment.AmountInPence, payment.CardToken),
            cancellationToken);
        payment.Record(reply, time.GetUtcNow(), policy);
        await db.SaveChangesAsync(cancellationToken);

        if (payment.Outcome is PaymentOutcome.Declined or PaymentOutcome.Failed)
        {
            await ordering.RecordPaymentOutcomeAsync(payment.OrderId, payment.Outcome, cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "A payment retry pass failed; trying again after the first back-off.")]
    private partial void LogRetryPassFailed(Exception exception);
}

/// <summary>
/// How Payments retries a payment whose outcome is unknown: the first back-off, doubled after each lost answer, and
/// how many resubmissions it makes before it gives up.
/// </summary>
internal sealed record PaymentRetryPolicy(TimeSpan FirstBackOff, int Retries, TimeSpan SubmissionLease)
{
    private const double BackOffGrowth = 2;

    /// <summary>
    /// Returns how long to wait after the <paramref name="attempts"/>-th submission lost its answer, or
    /// <see langword="null"/> when no retries are left.
    /// </summary>
    internal TimeSpan? BackOffAfter(int attempts) =>
        attempts > Retries ? null : FirstBackOff * Math.Pow(BackOffGrowth, attempts - 1);
}

/// <summary>
/// Wakes the retry worker when Payments has a new payment to retry.
/// </summary>
internal sealed class PaymentRetrySignal
{
    private readonly Channel<bool> _wakeUps = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    internal void Wake() => _wakeUps.Writer.TryWrite(item: true);

    /// <summary>
    /// Waits until <paramref name="until"/> (forever when <see langword="null"/>) or a wake-up, whichever comes first.
    /// </summary>
    internal async Task WaitAsync(DateTimeOffset? until, TimeProvider time, CancellationToken cancellationToken)
    {
        var wait = until is { } due ? Max(due - time.GetUtcNow(), TimeSpan.Zero) : Timeout.InfiniteTimeSpan;
        using var timer = new CancellationTokenSource(wait, time);
        using var either = CancellationTokenSource.CreateLinkedTokenSource(timer.Token, cancellationToken);
        try
        {
            await _wakeUps.Reader.WaitToReadAsync(either.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The next payment fell due.
        }

        while (_wakeUps.Reader.TryRead(out _))
        {
            // Drain, so one wake-up costs one pass.
        }
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}

internal static class PaymentRetryRegistration
{
    private const int DefaultRetries = 5;
    private const int LeaseTimeouts = 2;
    private static readonly TimeSpan DefaultFirstBackOff = TimeSpan.FromSeconds(2);

    internal static void AddPaymentRetries(this IHostApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection("PaymentProvider:Retry");
        var providerTimeout = builder.Configuration.GetValue<TimeSpan?>("PaymentProvider:Timeout") ?? PaymentProviderRegistration.DefaultTimeout;
        builder.Services.AddSingleton(new PaymentRetryPolicy(
            section.GetValue<TimeSpan?>("FirstBackOff") ?? DefaultFirstBackOff,
            section.GetValue<int?>("Attempts") ?? DefaultRetries,
            SubmissionLease: providerTimeout * LeaseTimeouts));
        builder.Services.AddSingleton<PaymentRetrySignal>();
        builder.Services.AddHostedService<PaymentRetryWorker>();
    }
}
