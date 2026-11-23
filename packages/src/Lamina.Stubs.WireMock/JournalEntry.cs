using System.Globalization;
using System.Text.Json.Nodes;

namespace Lamina.Stubs.WireMock;

/// <summary>
/// One request WireMock received, with what it answered.
/// </summary>
public sealed class JournalEntry
{
    private readonly JsonObject _entry;

    internal JournalEntry(JsonObject entry) => _entry = entry;

    public DateTimeOffset ReceivedAt =>
        Request?["DateTime"]?.GetValue<DateTimeOffset>() ?? DateTimeOffset.MinValue;

    public string? Method => Request?["Method"]?.GetValue<string>();

    public string? Path => Request?["Path"]?.GetValue<string>();

    /// <summary>
    /// Gets the request's JSON body, or <see langword="null"/> when it had none.
    /// </summary>
    public JsonNode? Body => Request?["BodyAsJson"];

    /// <summary>
    /// Gets the status WireMock answered with, or <see langword="null"/> when it didn't answer.
    /// </summary>
    public int? Status => Response?["StatusCode"] switch
    {
        null => null,
        JsonValue value when value.TryGetValue<int>(out var number) => number,
        JsonNode value => int.Parse(value.ToString(), CultureInfo.InvariantCulture),
    };

    /// <summary>
    /// Gets the JSON body WireMock answered with, or <see langword="null"/> when it had none.
    /// </summary>
    public JsonNode? ResponseBody => Response?["BodyAsJson"];

    /// <summary>
    /// Gets a value indicating whether a fault spoiled the answer, so the caller got an empty or garbage body: the
    /// request reached the system, but its answer was lost.
    /// </summary>
    public bool AnswerLost =>
        Response?["FaultType"]?.ToString() is { Length: > 0 } fault && !string.Equals(fault, "NONE", StringComparison.OrdinalIgnoreCase);

    private JsonObject? Request => _entry["Request"] as JsonObject;

    private JsonObject? Response => _entry["Response"] as JsonObject;

    /// <summary>
    /// Returns the first value of the request header <paramref name="name"/>, ignoring case, or <see langword="null"/>.
    /// </summary>
    public string? Header(string name)
    {
        var header = (Request?["Headers"] as JsonObject)?
            .FirstOrDefault(h => string.Equals(h.Key, name, StringComparison.OrdinalIgnoreCase))
            .Value;
        return header switch
        {
            JsonArray values => values.FirstOrDefault()?.ToString(),
            null => null,
            _ => header.ToString(),
        };
    }

    public override string ToString() => _entry.ToJsonString();
}
