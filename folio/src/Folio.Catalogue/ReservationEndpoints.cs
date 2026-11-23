using Microsoft.EntityFrameworkCore;

namespace Folio.Catalogue;

internal static class ReservationEndpoints
{
    internal static void MapReservationEndpoints(this IEndpointRouteBuilder app)
    {
        var reservations = app.MapGroup("/reservations/{orderId:guid}");
        reservations.MapPut("/", ReserveAsync);
        reservations.MapPost("/release", (Guid orderId, CatalogueDbContext db, CancellationToken cancellationToken) =>
            SettleAsync(orderId, ReservationStatus.Released, db, cancellationToken));
        reservations.MapPost("/commit", (Guid orderId, CatalogueDbContext db, CancellationToken cancellationToken) =>
            SettleAsync(orderId, ReservationStatus.Committed, db, cancellationToken));
    }

    private static async Task<IResult> ReserveAsync(Guid orderId, ReserveRequest request, CatalogueDbContext db, CancellationToken cancellationToken)
    {
        if (request.IsInvalid)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal) { ["reservation"] = ["A reservation needs a book and a quantity of one or more."] });
        }

        var existing = await FindAsync(db, orderId, cancellationToken);
        if (existing is not null)
        {
            return Results.Ok(View(existing));
        }

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var stockReserved = await db.Books
                .Where(b => b.Id == request.BookId && b.OnHand - b.Reserved >= request.Quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.Reserved, b => b.Reserved + request.Quantity), cancellationToken);
            if (stockReserved == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return await db.Books.AnyAsync(b => b.Id == request.BookId, cancellationToken)
                    ? Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Not enough stock", detail: $"Book {request.BookId} has fewer than {request.Quantity} available.")
                    : Results.NotFound();
            }

            var recorded = await db.Database.ExecuteSqlAsync(
                $"INSERT INTO reservations (order_id, book_id, quantity, status) VALUES ({orderId}, {request.BookId}, {request.Quantity}, {nameof(ReservationStatus.Held)}) ON CONFLICT (order_id) DO NOTHING",
                cancellationToken);
            if (recorded == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Results.Ok(View((await FindAsync(db, orderId, cancellationToken))!));
            }

            await transaction.CommitAsync(cancellationToken);
            var reservation = new Reservation { OrderId = orderId, BookId = request.BookId, Quantity = request.Quantity, Status = ReservationStatus.Held };
            return Results.Created(new Uri($"/reservations/{orderId}", UriKind.Relative), View(reservation));
        });
    }

    private static async Task<IResult> SettleAsync(Guid orderId, ReservationStatus settled, CatalogueDbContext db, CancellationToken cancellationToken)
    {
        var reservation = await FindAsync(db, orderId, cancellationToken);
        if (reservation is null)
        {
            return Results.NotFound();
        }

        if (reservation.Status == ReservationStatus.Held)
        {
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                var moved = await db.Reservations
                    .Where(r => r.OrderId == orderId && r.Status == ReservationStatus.Held)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, settled), cancellationToken);
                if (moved == 1)
                {
                    await AdjustStockAsync(db, reservation, settled, cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            });
            reservation = (await FindAsync(db, orderId, cancellationToken))!;
        }

        return reservation.Status == settled
            ? Results.Ok(View(reservation))
            : Results.Problem(statusCode: StatusCodes.Status409Conflict, title: $"The reservation is already {reservation.Status}.");
    }

    private static Task<int> AdjustStockAsync(CatalogueDbContext db, Reservation reservation, ReservationStatus settled, CancellationToken cancellationToken)
    {
        var book = db.Books.Where(b => b.Id == reservation.BookId);
        return settled == ReservationStatus.Committed
            ? book.ExecuteUpdateAsync(
                s => s.SetProperty(b => b.Reserved, b => b.Reserved - reservation.Quantity).SetProperty(b => b.OnHand, b => b.OnHand - reservation.Quantity),
                cancellationToken)
            : book.ExecuteUpdateAsync(s => s.SetProperty(b => b.Reserved, b => b.Reserved - reservation.Quantity), cancellationToken);
    }

    private static Task<Reservation?> FindAsync(CatalogueDbContext db, Guid orderId, CancellationToken cancellationToken) =>
        db.Reservations.AsNoTracking().SingleOrDefaultAsync(r => r.OrderId == orderId, cancellationToken);

    private static ReservationView View(Reservation reservation) =>
        new(reservation.OrderId, reservation.BookId, reservation.Quantity, reservation.Status);

    internal sealed record ReserveRequest(Guid BookId, int Quantity)
    {
        internal bool IsInvalid => BookId == Guid.Empty || Quantity < 1;
    }

    internal sealed record ReservationView(Guid OrderId, Guid BookId, int Quantity, ReservationStatus Status);
}
