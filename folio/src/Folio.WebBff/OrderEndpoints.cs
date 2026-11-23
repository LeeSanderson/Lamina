using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace Folio.WebBff;

internal static class OrderEndpoints
{
    internal static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/api/orders").RequireAuthorization();
        orders.MapPost("/", PlaceOrderAsync);
        orders.MapGet("/{id:guid}", GetOrderAsync);
    }

    private static async Task<IResult> PlaceOrderAsync(PlaceOrderRequest request, OrderingClient ordering, CancellationToken cancellationToken)
    {
        using var response = await ordering.PlaceOrderAsync(request, cancellationToken);
        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                var invalid = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
                return Results.ValidationProblem(invalid?.Errors ?? new Dictionary<string, string[]>(StringComparer.Ordinal));
            case HttpStatusCode.Conflict:
                var conflict = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
                return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: conflict?.Title ?? "Not enough stock");
            case HttpStatusCode.NotFound:
                return Results.NotFound();
            default:
                response.EnsureSuccessStatusCode();
                var order = await response.Content.ReadFromJsonAsync<OrderView>(cancellationToken);
                return Results.Created(new Uri($"/api/orders/{order!.Id}", UriKind.Relative), order);
        }
    }

    private static async Task<IResult> GetOrderAsync(Guid id, OrderingClient ordering, CancellationToken cancellationToken)
    {
        using var response = await ordering.GetOrderAsync(id, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return Results.NotFound();
        }

        response.EnsureSuccessStatusCode();
        return Results.Ok(await response.Content.ReadFromJsonAsync<OrderView>(cancellationToken));
    }

    internal sealed record OrderView(Guid Id, Guid BookId, int Quantity, decimal Amount, string Status, string? Reason);
}

internal sealed record PlaceOrderRequest(Guid BookId, int Quantity, string CardToken);
