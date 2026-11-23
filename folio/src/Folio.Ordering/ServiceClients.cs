using System.Net;
using System.Text.Json.Serialization;

namespace Folio.Ordering;

internal static class ServiceClients
{
    internal static void AddServiceClients(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpClient<CustomersClient>(client => client.BaseAddress = ClientAddress(builder, "Customers"));
        builder.Services.AddHttpClient<CatalogueClient>(client => client.BaseAddress = ClientAddress(builder, "Catalogue"));
        builder.Services.AddHttpClient<PaymentsClient>(client => client.BaseAddress = ClientAddress(builder, "Payments"));
    }

    private static Uri ClientAddress(WebApplicationBuilder builder, string service) =>
        new(builder.Configuration.GetRequiredSection($"Clients:{service}").Value!);
}

internal sealed class CustomersClient(HttpClient http)
{
    internal async Task<Guid> EnsureCustomerAsync(string subject, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsync(new Uri($"customers/{Uri.EscapeDataString(subject)}", UriKind.Relative), content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        var customer = await response.Content.ReadFromJsonAsync<CustomerView>(cancellationToken);
        return customer!.Id;
    }

    private sealed record CustomerView(Guid Id);
}

internal sealed class CatalogueClient(HttpClient http)
{
    internal async Task<CatalogueBook?> GetBookAsync(Guid bookId, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri($"books/{bookId}", UriKind.Relative), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CatalogueBook>(cancellationToken);
    }

    internal async Task<ReservationResult> ReserveAsync(Guid orderId, Guid bookId, int quantity, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync(new Uri($"reservations/{orderId}", UriKind.Relative), new { bookId, quantity }, cancellationToken);
        return response.StatusCode switch
        {
            HttpStatusCode.Conflict => ReservationResult.NotEnoughStock,
            HttpStatusCode.NotFound => ReservationResult.NoSuchBook,
            _ => Reserved(response),
        };
    }

    internal Task ReleaseAsync(Guid orderId, CancellationToken cancellationToken) => SettleAsync(orderId, "release", cancellationToken);

    internal Task CommitAsync(Guid orderId, CancellationToken cancellationToken) => SettleAsync(orderId, "commit", cancellationToken);

    private static ReservationResult Reserved(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return ReservationResult.StockHeld;
    }

    private async Task SettleAsync(Guid orderId, string settlement, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync(new Uri($"reservations/{orderId}/{settlement}", UriKind.Relative), content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    internal sealed record CatalogueBook(Guid Id, decimal Price);
}

internal enum ReservationResult
{
    StockHeld,
    NotEnoughStock,
    NoSuchBook,
}

internal sealed class PaymentsClient(HttpClient http)
{
    internal async Task<PaymentAnswer> CreatePaymentAsync(Guid orderId, decimal amount, string cardToken, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(new Uri("payments", UriKind.Relative), new { orderId, amount, cardToken }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payment = await response.Content.ReadFromJsonAsync<PaymentView>(cancellationToken);
        return payment!.Outcome;
    }

    private sealed record PaymentView(string Reference, PaymentAnswer Outcome);
}

[JsonConverter(typeof(JsonStringEnumConverter<PaymentAnswer>))]
internal enum PaymentAnswer
{
    Pending,
    Declined,
    Confirmed,
    Failed,
    Unknown,
}
