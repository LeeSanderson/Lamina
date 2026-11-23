using System.Text.Json.Serialization;

namespace Folio.Payments;

public sealed class Payment
{
    private const decimal PenceInAPound = 100m;

    public Guid Id { get; init; }

    public Guid OrderId { get; init; }

    public required string Reference { get; init; }

    public Guid IdempotencyKey { get; init; }

    public decimal Amount { get; init; }

    public required string CardToken { get; init; }

    public PaymentStatus Status { get; set; }

    public string? ProviderPaymentId { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? NextAttemptAt { get; set; }

    public int Attempts { get; set; }

    internal long AmountInPence => decimal.ToInt64(decimal.Round(Amount * PenceInAPound, MidpointRounding.AwayFromZero));

    internal PaymentOutcome Outcome => Status switch
    {
        PaymentStatus.Pending => PaymentOutcome.Pending,
        PaymentStatus.Declined => PaymentOutcome.Declined,
        PaymentStatus.Confirmed => PaymentOutcome.Confirmed,
        PaymentStatus.Failed => PaymentOutcome.Failed,
        _ => PaymentOutcome.Unknown,
    };

    // False, changing nothing, when the payment was declined.
    internal bool Confirm(string? providerPaymentId)
    {
        if (Status == PaymentStatus.Declined)
        {
            return false;
        }

        Status = PaymentStatus.Confirmed;
        ProviderPaymentId ??= providerPaymentId;
        NextAttemptAt = null;
        return true;
    }

    // The lease holds the payment back from the retry worker for longer than the provider call can take.
    internal void StartAttempt(DateTimeOffset now, TimeSpan lease)
    {
        Attempts++;
        NextAttemptAt = now + lease;
    }

    internal void Record(ProviderReply reply, DateTimeOffset now, PaymentRetryPolicy policy)
    {
        switch (reply.Answer)
        {
            case ProviderAnswer.Pending:
                Status = PaymentStatus.Pending;
                ProviderPaymentId = reply.ProviderPaymentId;
                NextAttemptAt = null;
                break;
            case ProviderAnswer.Declined:
                Status = PaymentStatus.Declined;
                NextAttemptAt = null;
                break;
            case ProviderAnswer.Unknown when policy.BackOffAfter(Attempts) is { } backOff:
                NextAttemptAt = now + backOff;
                break;
            case ProviderAnswer.Unknown:
                Status = PaymentStatus.Failed;
                NextAttemptAt = null;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(reply), reply.Answer, "Unknown provider answer.");
        }
    }
}

public enum PaymentStatus
{
    AwaitingSubmission,
    Pending,
    Declined,
    Confirmed,
    Failed,
}

[JsonConverter(typeof(JsonStringEnumConverter<PaymentOutcome>))]
public enum PaymentOutcome
{
    Pending,
    Declined,
    Confirmed,
    Failed,
    Unknown,
}
