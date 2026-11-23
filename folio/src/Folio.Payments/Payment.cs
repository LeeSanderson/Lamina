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

    internal long AmountInPence => decimal.ToInt64(decimal.Round(Amount * PenceInAPound, MidpointRounding.AwayFromZero));

    internal PaymentOutcome Outcome => Status switch
    {
        PaymentStatus.Pending => PaymentOutcome.Pending,
        PaymentStatus.Declined => PaymentOutcome.Declined,
        PaymentStatus.Confirmed => PaymentOutcome.Confirmed,
        _ => PaymentOutcome.Unknown,
    };

    /// <summary>
    /// Records that the provider confirmed the payment, under its id <paramref name="providerPaymentId"/>. Returns false,
    /// changing nothing, when the payment was declined.
    /// </summary>
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

    internal void Record(ProviderReply reply)
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
            case ProviderAnswer.Unknown:
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
}

[JsonConverter(typeof(JsonStringEnumConverter<PaymentOutcome>))]
public enum PaymentOutcome
{
    Pending,
    Declined,
    Confirmed,
    Unknown,
}
