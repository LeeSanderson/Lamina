using Microsoft.EntityFrameworkCore;

namespace Folio.Ordering;

internal sealed class OrderBook(
    OrderingDbContext db,
    CustomersClient customers,
    CatalogueClient catalogue,
    PaymentsClient payments,
    TimeProvider time)
{
    internal async Task<Placement> PlaceAsync(string subject, Guid bookId, int quantity, string cardToken, CancellationToken cancellationToken)
    {
        var book = await catalogue.GetBookAsync(bookId, cancellationToken);
        if (book is null)
        {
            return Placement.NoSuchBook;
        }

        var customerId = await customers.EnsureCustomerAsync(subject, cancellationToken);
        var orderId = Guid.CreateVersion7();
        var reservation = await catalogue.ReserveAsync(orderId, bookId, quantity, cancellationToken);
        if (reservation != ReservationResult.StockHeld)
        {
            return reservation == ReservationResult.NotEnoughStock ? Placement.NotEnoughStock : Placement.NoSuchBook;
        }

        var order = new Order
        {
            Id = orderId,
            CustomerId = customerId,
            BookId = bookId,
            Quantity = quantity,
            Amount = book.Price * quantity,
            Status = OrderStatus.Pending,
            PlacedAt = time.GetUtcNow(),
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);

        var payment = await payments.CreatePaymentAsync(orderId, order.Amount, cardToken, cancellationToken);
        if (payment == PaymentAnswer.Declined)
        {
            order = await ApplyOutcomeAsync(order, PaymentOutcome.Declined, cancellationToken);
        }

        return Placement.Placed(order);
    }

    internal async Task<Order?> FindForSubjectAsync(Guid id, string subject, CancellationToken cancellationToken)
    {
        var customerId = await customers.EnsureCustomerAsync(subject, cancellationToken);
        return await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id && o.CustomerId == customerId, cancellationToken);
    }

    internal async Task<Order?> RecordPaymentOutcomeAsync(Guid id, PaymentOutcome outcome, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id, cancellationToken);
        return order is null ? null : await ApplyOutcomeAsync(order, outcome, cancellationToken);
    }

    private async Task<Order> ApplyOutcomeAsync(Order order, PaymentOutcome outcome, CancellationToken cancellationToken)
    {
        if (order.Status != OrderStatus.Pending)
        {
            return order;
        }

        var (status, reason) = outcome switch
        {
            PaymentOutcome.Confirmed => (OrderStatus.Confirmed, default(RejectionReason?)),
            PaymentOutcome.Declined => (OrderStatus.Rejected, RejectionReason.PaymentDeclined),
            PaymentOutcome.Failed => (OrderStatus.Rejected, RejectionReason.PaymentFailed),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown payment outcome."),
        };

        // Catalogue first: its release and commit are idempotent, so if the status update below never
        // happens, the next outcome for this order settles the stock again harmlessly.
        if (status == OrderStatus.Confirmed)
        {
            await catalogue.CommitAsync(order.Id, cancellationToken);
        }
        else
        {
            await catalogue.ReleaseAsync(order.Id, cancellationToken);
        }

        await db.Orders
            .Where(o => o.Id == order.Id && o.Status == OrderStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, status).SetProperty(o => o.RejectionReason, reason), cancellationToken);
        return await db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id, cancellationToken);
    }
}

internal sealed record Placement(Order? Order, PlacementFailure? Failure)
{
    internal static Placement NoSuchBook { get; } = new(Order: null, PlacementFailure.NoSuchBook);

    internal static Placement NotEnoughStock { get; } = new(Order: null, PlacementFailure.NotEnoughStock);

    internal static Placement Placed(Order order) => new(order, Failure: null);
}

internal enum PlacementFailure
{
    NoSuchBook,
    NotEnoughStock,
}
