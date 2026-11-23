using Microsoft.EntityFrameworkCore;

namespace Folio.Payments;

internal static class ProviderWebhookEndpoints
{
    private const string PaymentSucceeded = "payment.succeeded";

    internal static void MapProviderWebhookEndpoints(this IEndpointRouteBuilder app) =>
        app.MapPost("/webhooks/payment-provider", ReceiveAsync);

    // The provider redelivers a webhook until it gets a 2xx, so every step is safe to repeat: a confirmed payment
    // stays confirmed, and Ordering ignores an outcome for an order that's already settled. Ordering is told on every
    // delivery, so a delivery that confirmed the payment but couldn't reach Ordering is finished by the next one.
    private static async Task<IResult> ReceiveAsync(
        ProviderWebhook webhook,
        PaymentsDbContext db,
        OrderingClient ordering,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(webhook.Type, PaymentSucceeded, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(webhook.Reference))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal) { ["webhook"] = [$"Only {PaymentSucceeded} webhooks with a reference are handled."] });
        }

        var payment = await db.Payments.SingleOrDefaultAsync(p => p.Reference == webhook.Reference, cancellationToken);
        if (payment is null)
        {
            return Results.NotFound();
        }

        if (!payment.Confirm(webhook.Id))
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: $"Payment {payment.Reference} was {payment.Status}, so it can't be confirmed.");
        }

        await db.SaveChangesAsync(cancellationToken);
        await ordering.RecordPaymentOutcomeAsync(payment.OrderId, PaymentOutcome.Confirmed, cancellationToken);
        return Results.Ok();
    }

    internal sealed record ProviderWebhook(string? Type, string? Id, string? Reference);
}
