using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lamina.Stubs.WireMock;

/// <summary>
/// A canned answer: a status and, optionally, a JSON body.
/// </summary>
public sealed record StubResponse
{
    public StubResponse(int status)
    {
        Status = status;
    }

    public StubResponse(int status, object body)
    {
        Status = status;
        Body = body;
    }

    public int Status { get; }

    public object? Body { get; }

    /// <summary>
    /// Gets a value indicating whether the body is a Handlebars template over the request, such as
    /// <c>pay_{{Random Type="Guid"}}</c>.
    /// </summary>
    public bool Templated { get; init; }

    internal JsonObject ToJson()
    {
        var json = new JsonObject { ["StatusCode"] = Status };
        if (Body is not null)
        {
            json["BodyAsJson"] = JsonSerializer.SerializeToNode(Body);
        }

        if (Templated)
        {
            json["UseTransformer"] = true;
        }

        return json;
    }
}
