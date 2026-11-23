using Microsoft.AspNetCore.Mvc;

namespace Folio.Ordering;

internal static class OrderEndpoints
{
    private const string SubjectHeader = "X-Folio-Subject";

    internal static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/orders");
        orders.MapPost("/", PlaceOrderAsync);
        orders.MapGet("/{id:guid}", GetOrderAsync);
        orders.MapPost("/{id:guid}/payment-outcome", RecordPaymentOutcomeAsync);
    }

    private static async Task<IResult> PlaceOrderAsync(
        [FromHeader(Name = SubjectHeader)] string? subject,
        PlaceOrderRequest request,
        OrderBook orders,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subject) || request.IsInvalid)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal) { ["order"] = ["An order needs a customer, a book, a quantity of one or more and a card token."] });
        }

        var placement = await orders.PlaceAsync(subject, request.BookId, request.Quantity, request.CardToken, cancellationToken);
        return placement switch
        {
            { Order: { } order } => Results.Created(new Uri($"/orders/{order.Id}", UriKind.Relative), View(order)),
            { Failure: PlacementFailure.NotEnoughStock } => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Not enough stock",
                detail: $"Book {request.BookId} has fewer than {request.Quantity} available."),
            _ => Results.NotFound(),
        };
    }

    private static async Task<IResult> GetOrderAsync(
        Guid id,
        [FromHeader(Name = SubjectHeader)] string? subject,
        OrderBook orders,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Results.NotFound();
        }

        var order = await orders.FindForSubjectAsync(id, subject, cancellationToken);
        return order is null ? Results.NotFound() : Results.Ok(View(order));
    }

    private static async Task<IResult> RecordPaymentOutcomeAsync(
        Guid id,
        PaymentOutcomeRequest request,
        OrderBook orders,
        CancellationToken cancellationToken)
    {
        var order = await orders.RecordPaymentOutcomeAsync(id, request.Outcome, cancellationToken);
        return order is null ? Results.NotFound() : Results.Ok(View(order));
    }

    private static OrderView View(Order order) =>
        new(order.Id, order.BookId, order.Quantity, order.Amount, order.Status, order.RejectionReason);

    internal sealed record PlaceOrderRequest(Guid BookId, int Quantity, string CardToken)
    {
        internal bool IsInvalid => BookId == Guid.Empty || Quantity < 1 || string.IsNullOrWhiteSpace(CardToken);
    }

    internal sealed record PaymentOutcomeRequest(PaymentOutcome Outcome);

    internal sealed record OrderView(Guid Id, Guid BookId, int Quantity, decimal Amount, OrderStatus Status, RejectionReason? Reason);
}
