using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json;

namespace Lamina.Drivers.Http;

/// <summary>
/// Reads responses the way drivers need them: a success as JSON, or a failure as problem details.
/// </summary>
[SuppressMessage("Critical Code Smell", "S4018:Generic methods should provide type parameters", Justification = "Each reads the body as the type the caller states, like ReadFromJsonAsync<T>.")]
public static class HttpResponseMessageExtensions
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;

    /// <summary>
    /// Reads a successful response's JSON body as <typeparamref name="T"/>. A failed response throws an
    /// <see cref="HttpProblemException"/> that shows its status and, when it has one, its problem details.
    /// </summary>
    public static Task<T> ReadJsonAsync<T>(this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        return ReadJsonCoreAsync<T>(response, cancellationToken);
    }

    /// <summary>
    /// Reads a failed response's problem details. A successful response throws, since a driver that expects a problem
    /// should say so when there isn't one.
    /// </summary>
    public static Task<HttpProblem> ReadProblemAsync(this HttpResponseMessage response, CancellationToken cancellationToken) =>
        response.ReadProblemAsync<HttpProblem>(cancellationToken);

    /// <summary>
    /// Reads a failed response's problem details as the team's own <typeparamref name="TProblem"/>, whose members match the
    /// standard members and extensions it needs.
    /// </summary>
    public static Task<TProblem> ReadProblemAsync<TProblem>(this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        return ReadProblemCoreAsync<TProblem>(response, cancellationToken);
    }

    private static async Task<T> ReadJsonCoreAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var problem = TryReadProblem(body);
            throw new HttpProblemException(
                $"{Describe(response)} answered {Status(response)}, not a success. {problem?.Detail ?? problem?.Title ?? body}".TrimEnd(),
                response.StatusCode,
                problem);
        }

        return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken).ConfigureAwait(false)
            ?? throw new HttpRequestException($"{Describe(response)} answered {Status(response)} with a null body, not a {typeof(T).Name}.");
    }

    private static async Task<TProblem> ReadProblemCoreAsync<TProblem>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{Describe(response)} answered {Status(response)}, not a problem.", inner: null, response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return JsonSerializer.Deserialize<TProblem>(body, Json)
                ?? throw new HttpRequestException($"{Describe(response)} answered {Status(response)} with a null body, not problem details.");
        }
        catch (JsonException e)
        {
            throw new HttpRequestException($"{Describe(response)} answered {Status(response)}, but its body isn't problem details: {body}", e, response.StatusCode);
        }
    }

    private static HttpProblem? TryReadProblem(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<HttpProblem>(body, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Describe(HttpResponseMessage response) =>
        response.RequestMessage is { } request ? $"{request.Method} {request.RequestUri}" : "The request";

    private static string Status(HttpResponseMessage response) => $"{response.StatusCode:D} {response.ReasonPhrase}";
}
