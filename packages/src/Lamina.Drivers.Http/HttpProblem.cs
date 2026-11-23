using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lamina.Drivers.Http;

/// <summary>
/// An RFC 9457 problem details response, as ASP.NET Core's <c>Results.Problem</c> writes it. Members beyond the standard
/// ones are in <see cref="Extensions"/>.
/// </summary>
public sealed record HttpProblem
{
    public string? Type { get; init; }

    public string? Title { get; init; }

    public int? Status { get; init; }

    public string? Detail { get; init; }

    public string? Instance { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> Extensions { get; init; } = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
}
