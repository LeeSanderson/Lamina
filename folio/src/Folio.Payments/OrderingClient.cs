namespace Folio.Payments;

internal sealed class OrderingClient(HttpClient http)
{
    // Ordering ignores an outcome for an order that's already settled, so this is safe to repeat.
    internal async Task RecordPaymentConfirmedAsync(Guid orderId, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            new Uri($"orders/{orderId}/payment-outcome", UriKind.Relative),
            new { outcome = "Confirmed" },
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

internal static class OrderingClientRegistration
{
    internal static void AddOrderingClient(this IHostApplicationBuilder builder) =>
        builder.Services.AddHttpClient<OrderingClient>(http =>
            http.BaseAddress = new Uri(builder.Configuration.GetRequiredSection("Clients:Ordering").Value!));
}
