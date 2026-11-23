using System.Text.Json.Serialization;

namespace Folio.Ordering;

public sealed class Order
{
    public Guid Id { get; init; }

    public Guid CustomerId { get; init; }

    public Guid BookId { get; init; }

    public int Quantity { get; init; }

    public decimal Amount { get; init; }

    public OrderStatus Status { get; init; }

    public RejectionReason? RejectionReason { get; init; }

    public DateTimeOffset PlacedAt { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<OrderStatus>))]
public enum OrderStatus
{
    Pending,
    Confirmed,
    Rejected,
}

[JsonConverter(typeof(JsonStringEnumConverter<RejectionReason>))]
public enum RejectionReason
{
    PaymentDeclined,
    PaymentFailed,
}

[JsonConverter(typeof(JsonStringEnumConverter<PaymentOutcome>))]
public enum PaymentOutcome
{
    Confirmed,
    Declined,
    Failed,
}
