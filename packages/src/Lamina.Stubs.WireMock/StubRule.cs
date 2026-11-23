using System.Diagnostics.CodeAnalysis;

namespace Lamina.Stubs.WireMock;

/// <summary>
/// A rule being built for one request description. Each terminal call installs one WireMock mapping.
/// </summary>
[SuppressMessage("Minor Code Smell", "S4261:Methods should be named according to their synchronicities", Justification = "Reads as a sentence in a team stub: When(request).Respond(...) (Plan.md §6.5).")]
public sealed class StubRule
{
    private readonly WireMockStub _stub;
    private readonly StubRequest _request;

    internal StubRule(WireMockStub stub, StubRequest request)
    {
        _stub = stub;
        _request = request;
    }

    public Task Respond(StubResponse response) => _stub.AddRuleAsync(_request, response);

    public Task Respond(int status) => Respond(new StubResponse(status));

    public Task Respond(int status, object body) => Respond(new StubResponse(status, body));
}
