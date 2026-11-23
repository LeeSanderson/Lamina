using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Nodes;

namespace Lamina.Stubs.WireMock;

/// <summary>
/// A <see cref="LaminaStub"/> over WireMock.Net's admin REST API. A team stub composes the protected building blocks
/// into domain verbs, so DSLs and specs never see WireMock's own terms.
/// </summary>
public abstract class WireMockStub : LaminaStub
{
    // In WireMock a lower number wins: a spec's rules beat the run's defaults.
    private const int RulePriority = 5;
    private const int DefaultPriority = 1000;

    protected static StubRequest Post(string path) => new("POST", path);

    protected static StubRequest Get(string path) => new("GET", path);

    protected static StubRequest Put(string path) => new("PUT", path);

    protected static StubRequest Delete(string path) => new("DELETE", path);

    /// <summary>
    /// Starts a rule for this spec. The request must be keyed to the spec through an aliased value.
    /// </summary>
    protected StubRule When(StubRequest request) => new(this, request);

    /// <summary>
    /// Returns this spec's requests that match <paramref name="request"/>, oldest first. The request must be keyed to
    /// the spec, so the journal never returns another spec's requests.
    /// </summary>
    [SuppressMessage("Minor Code Smell", "S4261:Methods should be named according to their synchronicities", Justification = "Named by the stub abstraction design (Plan.md §6.5).")]
    protected async Task<IReadOnlyList<JournalEntry>> Journal(StubRequest request)
    {
        var matcher = request.ToJson();
        RequireKeyedToSpec($"journal query {request}", matcher.ToJsonString());
        using var response = await Http.PostAsync(Admin("requests/find"), Json(matcher)).ConfigureAwait(false);
        var found = await ReadSuccessAsync(response, "search the journal").ConfigureAwait(false);
        var entries = JsonNode.Parse(found) as JsonArray ?? [];
        return [.. entries.OfType<JsonObject>().Select(e => new JournalEntry(e)).OrderBy(e => e.ReceivedAt)];
    }

    /// <summary>
    /// Installs a run-level rule that matches anyone's requests, under the fixed <paramref name="id"/>, so installing
    /// it again replaces it. Only allowed from <see cref="LaminaStub.InstallDefaults"/>.
    /// </summary>
    [SuppressMessage("Minor Code Smell", "S4261:Methods should be named according to their synchronicities", Justification = "Named by the stub abstraction design (Plan.md §6.5).")]
    protected Task Default(Guid id, StubRequest request, StubResponse response)
    {
        RequireRunStart($"default {request}");
        return PostMappingAsync(Mapping(id, $"lamina:default:{GetType().Name}", DefaultPriority, request, response));
    }

    protected override async Task DeleteRulesAsync(IReadOnlyList<string> ids)
    {
        var guids = new JsonArray([.. ids.Select<string, JsonNode?>(id => new JsonObject { ["Guid"] = id })]);
        using var delete = new HttpRequestMessage(HttpMethod.Delete, Admin("mappings")) { Content = Json(guids) };
        using var response = await Http.SendAsync(delete).ConfigureAwait(false);
        await ReadSuccessAsync(response, "delete this spec's rules").ConfigureAwait(false);
    }

    internal async Task AddRuleAsync(StubRequest request, StubResponse response)
    {
        var matcher = request.ToJson();
        RequireKeyedToSpec($"rule {request}", matcher.ToJsonString());
        var id = Guid.NewGuid();
        TrackRule(id.ToString());
        await PostMappingAsync(Mapping(id, $"lamina:{TestId}:{GetType().Name}", RulePriority, request, response)).ConfigureAwait(false);
    }

    private static JsonObject Mapping(Guid id, string title, int priority, StubRequest request, StubResponse response) => new()
    {
        ["Guid"] = id.ToString(),
        ["Title"] = title,
        ["Priority"] = priority,
        ["Request"] = request.ToJson(),
        ["Response"] = response.ToJson(),
    };

    private static StringContent Json(JsonNode body) => new(body.ToJsonString(), Encoding.UTF8, "application/json");

    private static async Task<string> ReadSuccessAsync(HttpResponseMessage response, string what)
    {
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return response.IsSuccessStatusCode
            ? body
            : throw new InvalidOperationException($"WireMock refused to {what} ({response.StatusCode:D}): {body}");
    }

    private async Task PostMappingAsync(JsonObject mapping)
    {
        using var response = await Http.PostAsync(Admin("mappings"), Json(mapping)).ConfigureAwait(false);
        await ReadSuccessAsync(response, $"add mapping {mapping.ToJsonString()}").ConfigureAwait(false);
    }

    private Uri Admin(string path) => new(Endpoint, $"__admin/{path}");
}
