using System.Text.Json.Serialization;

namespace Folio.Catalogue;

public sealed class Reservation
{
    public Guid OrderId { get; init; }

    public Guid BookId { get; init; }

    public int Quantity { get; init; }

    public ReservationStatus Status { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<ReservationStatus>))]
public enum ReservationStatus
{
    Held,
    Released,
    Committed,
}
