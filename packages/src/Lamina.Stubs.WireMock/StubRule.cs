using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

namespace Lamina.Stubs.WireMock;

/// <summary>
/// A rule being built for one request description. Each terminal call installs one WireMock mapping. Faults beat the
/// spec's other rules and the run defaults.
/// </summary>
[SuppressMessage("Minor Code Smell", "S4261:Methods should be named according to their synchronicities", Justification = "Reads as a sentence in a team stub: When(request).Respond(...) (Plan.md §6.5).")]
public sealed class StubRule
{
    /// <summary>
    /// How long <see cref="WillTimeOut"/> holds the answer back: longer than any sensible client timeout.
    /// </summary>
    public static readonly TimeSpan TimeOutDelay = TimeSpan.FromMinutes(2);

    private const int GatewayTimeout = 504;

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

    /// <summary>
    /// Answers <paramref name="status"/> with no body <paramref name="times"/> times, then falls through to the spec's
    /// other rules or the run default.
    /// </summary>
    public Task WillFail(int status, int times) =>
        Fault(new StubResponse(status).ToJson(), RequirePositive(times));

    /// <summary>
    /// Answers <paramref name="status"/> with an empty body <paramref name="times"/> times, at once: the request
    /// reached the system, but its answer was lost. The journal sees the request straight away.
    /// </summary>
    public Task WillAnswerEmpty(int status, int times) =>
        Fault(FaultResponse(status, "EMPTY_RESPONSE"), RequirePositive(times));

    /// <summary>
    /// Answers <paramref name="status"/> with a garbage body <paramref name="times"/> times, at once. The journal sees
    /// the request straight away.
    /// </summary>
    public Task WillAnswerMalformed(int status, int times) =>
        Fault(FaultResponse(status, "MALFORMED_RESPONSE_CHUNK"), RequirePositive(times));

    /// <summary>
    /// Holds every answer back for <see cref="TimeOutDelay"/>, so the caller's own timeout fires first. That costs the
    /// caller a full timeout, and the journal only sees the request when the hold ends.
    /// </summary>
    public Task WillTimeOut() => Fault(Delayed(new StubResponse(GatewayTimeout).ToJson(), TimeOutDelay), times: null);

    /// <summary>
    /// Holds every answer back for <paramref name="delay"/>, then answers <paramref name="then"/>. The journal only
    /// sees the request when the hold ends.
    /// </summary>
    public Task WillDelay(TimeSpan delay, StubResponse then)
    {
        ArgumentNullException.ThrowIfNull(then);
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);
        return Fault(Delayed(then.ToJson(), delay), times: null);
    }

    private static int RequirePositive(int times)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(times, 1);
        return times;
    }

    private static JsonObject FaultResponse(int status, string fault) => new()
    {
        ["StatusCode"] = status,
        ["Fault"] = new JsonObject { ["Type"] = fault },
    };

    private static JsonObject Delayed(JsonObject response, TimeSpan delay)
    {
        response["Delay"] = delay.Ticks / TimeSpan.TicksPerMillisecond;
        return response;
    }

    private Task Fault(JsonObject response, int? times) => _stub.AddFaultAsync(_request, response, times);
}
