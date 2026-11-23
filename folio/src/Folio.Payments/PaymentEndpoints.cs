using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Folio.Payments;

internal static class PaymentEndpoints
{
    internal static void MapPaymentEndpoints(this IEndpointRouteBuilder app) =>
        app.MapPost("/payments", CreatePaymentAsync);

    private static async Task<IResult> CreatePaymentAsync(
        CreatePaymentRequest request,
        PaymentsDbContext db,
        PaymentProviderClient provider,
        PaymentRetryPolicy policy,
        PaymentRetrySignal retries,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (request.IsInvalid)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal) { ["payment"] = ["A payment needs an order, an amount above zero and a card token."] });
        }

        var existing = await FindForOrderAsync(db, request.OrderId, cancellationToken);
        if (existing is not null)
        {
            return Results.Ok(View(existing));
        }

        var payment = NewPayment(request, time.GetUtcNow(), policy);
        if (!await TryRecordAsync(db, payment, cancellationToken))
        {
            return Results.Ok(View((await FindForOrderAsync(db, request.OrderId, cancellationToken))!));
        }

        var reply = await provider.CreatePaymentAsync(
            new PaymentCreate(payment.Reference, payment.IdempotencyKey, payment.AmountInPence, payment.CardToken),
            cancellationToken);
        payment.Record(reply, time.GetUtcNow(), policy);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The webhook settled the payment while the create was in flight; its outcome stands.
            db.ChangeTracker.Clear();
            return Results.Ok(View((await FindForOrderAsync(db, request.OrderId, cancellationToken))!));
        }

        if (payment.Status == PaymentStatus.AwaitingSubmission)
        {
            retries.Wake();
        }

        return Results.Ok(View(payment));
    }

    // Recorded as already submitted once, and leased so the retry worker leaves it alone while this call is in flight.
    private static Payment NewPayment(CreatePaymentRequest request, DateTimeOffset now, PaymentRetryPolicy policy)
    {
        var id = Guid.CreateVersion7();
        return new Payment
        {
            Id = id,
            OrderId = request.OrderId,
            Reference = $"folio-{id:N}",
            IdempotencyKey = Guid.NewGuid(),
            Amount = request.Amount,
            CardToken = request.CardToken,
            Status = PaymentStatus.AwaitingSubmission,
            CreatedAt = now,
            Attempts = 1,
            NextAttemptAt = now + policy.SubmissionLease,
        };
    }

    private static async Task<bool> TryRecordAsync(PaymentsDbContext db, Payment payment, CancellationToken cancellationToken)
    {
        db.Payments.Add(payment);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    private static Task<Payment?> FindForOrderAsync(PaymentsDbContext db, Guid orderId, CancellationToken cancellationToken) =>
        db.Payments.SingleOrDefaultAsync(p => p.OrderId == orderId, cancellationToken);

    private static PaymentView View(Payment payment) => new(payment.Reference, payment.Outcome);

    internal sealed record CreatePaymentRequest(Guid OrderId, decimal Amount, string CardToken)
    {
        internal bool IsInvalid => OrderId == Guid.Empty || Amount <= 0 || string.IsNullOrWhiteSpace(CardToken);
    }

    internal sealed record PaymentView(string Reference, PaymentOutcome Outcome);
}
