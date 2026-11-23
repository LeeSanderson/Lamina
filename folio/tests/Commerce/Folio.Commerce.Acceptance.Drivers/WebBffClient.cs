using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Lamina.Drivers.Http;

namespace Folio.Commerce.Acceptance.Drivers;

[SuppressMessage("Critical Code Smell", "S4018:Generic methods should provide type parameters", Justification = "Each reads the body as the type the caller states, like ReadFromJsonAsync<T>.")]
internal sealed class WebBffClient(HttpClient http, ApiSession session)
{
    internal async Task<T> GetAsync<T>(string path)
    {
        using var request = Request(HttpMethod.Get, path);
        using var response = await http.SendAsync(request);
        return await response.ReadJsonAsync<T>(CancellationToken.None);
    }

    internal async Task<T> PostAsync<T>(string path, object body)
    {
        using var request = Request(HttpMethod.Post, path);
        request.Content = JsonContent.Create(body);
        using var response = await http.SendAsync(request);
        return await response.ReadJsonAsync<T>(CancellationToken.None);
    }

    private HttpRequestMessage Request(HttpMethod method, string path) =>
        new(method, new Uri(path, UriKind.Relative))
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", session.Token) },
        };
}
