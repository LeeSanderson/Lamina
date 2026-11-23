namespace Folio.WebBff;

internal sealed class OrderingClient(HttpClient http)
{
    internal async Task<HttpResponseMessage> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken cancellationToken) =>
        await http.PostAsJsonAsync(new Uri("orders", UriKind.Relative), request, cancellationToken);

    internal async Task<HttpResponseMessage> GetOrderAsync(Guid id, CancellationToken cancellationToken) =>
        await http.GetAsync(new Uri($"orders/{id}", UriKind.Relative), cancellationToken);
}
