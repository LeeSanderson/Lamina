using System.Net;

namespace Folio.WebBff;

internal sealed class CatalogueClient(HttpClient http)
{
    internal async Task<HttpResponseMessage> AddBookAsync(AddBookRequest request, CancellationToken cancellationToken) =>
        await http.PostAsJsonAsync(new Uri("books", UriKind.Relative), request, cancellationToken);

    internal async Task<CatalogueBook?> GetBookAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri($"books/{id}", UriKind.Relative), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CatalogueBook>(cancellationToken);
    }

    internal sealed record CatalogueBook(Guid Id, string Title, decimal Price, int AvailableQuantity);
}
