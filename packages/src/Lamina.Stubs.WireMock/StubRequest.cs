using System.Collections.Immutable;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Lamina.Stubs.WireMock;

/// <summary>
/// What a request looks like: its method, path and body fields. The same description is a rule's matcher and a
/// journal query, so what was stubbed and what is asked of the journal can't drift apart.
/// </summary>
public sealed record StubRequest
{
    private ImmutableSortedDictionary<string, string> _bodyFields = ImmutableSortedDictionary.Create<string, string>(StringComparer.Ordinal);

    internal StubRequest(string method, string path)
    {
        Method = method;
        Path = path;
    }

    public string Method { get; }

    public string Path { get; }

    /// <summary>
    /// Matches requests whose top-level JSON body field <paramref name="field"/> contains <paramref name="value"/>,
    /// such as an aliased card token.
    /// </summary>
    public StubRequest WithBodyField(string field, string value) => this with { _bodyFields = _bodyFields.SetItem(field, value) };

    public override string ToString() =>
        _bodyFields.IsEmpty
            ? $"{Method} {Path}"
            : $"{Method} {Path} where {string.Join(", ", _bodyFields.Select(f => $"{f.Key} contains '{f.Value}'"))}";

    internal JsonObject ToJson()
    {
        var json = new JsonObject
        {
            ["Methods"] = new JsonArray(Method),
            ["Path"] = new JsonObject
            {
                ["Matchers"] = new JsonArray(new JsonObject { ["Name"] = "ExactMatcher", ["Pattern"] = Path }),
            },
        };
        if (!_bodyFields.IsEmpty)
        {
            var pattern = new JsonObject();
            foreach (var (field, value) in _bodyFields)
            {
                pattern[field] = Regex.Escape(value);
            }

            json["Body"] = new JsonObject
            {
                ["Matcher"] = new JsonObject { ["Name"] = "JsonPartialMatcher", ["Pattern"] = pattern, ["Regex"] = true },
            };
        }

        return json;
    }
}
