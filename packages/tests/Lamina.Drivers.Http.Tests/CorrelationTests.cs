using System.Net.Http.Json;

namespace Lamina.Drivers.Http.Tests;

public sealed class CorrelationTests
{
    [Fact]
    public async Task EveryRequestCarriesTheSpecsTestIdAsBaggage()
    {
        await using var run = await HttpSuite.StartAsync(lamina => lamina.AddHttpClient("web-bff"));
        await using var first = run.CreateScope(channel: null, specName: "first");
        await using var second = run.CreateScope(channel: null, specName: "second");

        var firstEcho = await GetEchoAsync(first);
        var secondEcho = await GetEchoAsync(second);

        Baggage(firstEcho).ShouldBe([new("lamina.test", first.TestId)]);
        Baggage(secondEcho).ShouldBe([new("lamina.test", second.TestId)]);
    }

    private static async Task<Echo> GetEchoAsync(SpecScope scope)
    {
        var client = scope.Services.GetRequiredService<SutHttpClients>().For("web-bff");
        return (await client.GetFromJsonAsync<Echo>("api/books", TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    // W3C baggage allows whitespace around '=' and ',', so it's parsed, never compared as a string. See docs/spikes/s2-correlation.md.
    private static List<KeyValuePair<string, string>> Baggage(Echo echo) =>
        echo.Baggage
            .SelectMany(header => (header ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(member => member.Split('=', 2, StringSplitOptions.TrimEntries))
            .Select(pair => new KeyValuePair<string, string>(Uri.UnescapeDataString(pair[0]), Uri.UnescapeDataString(pair[1])))
            .ToList();
}
