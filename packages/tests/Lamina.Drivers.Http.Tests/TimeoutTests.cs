namespace Lamina.Drivers.Http.Tests;

public sealed class TimeoutTests
{
    [Fact]
    public async Task TheClientTimeoutIsHttpClientsDefaultWhenNoMultiplierIsSet()
    {
        var client = await ClientAsync(multiplier: null);

        client.Timeout.ShouldBe(TimeSpan.FromSeconds(100));
    }

    [Fact]
    public async Task TheClientTimeoutIsScaledByTheTimeoutMultiplier()
    {
        var client = await ClientAsync(multiplier: "2.5");

        client.Timeout.ShouldBe(TimeSpan.FromSeconds(250));
    }

    [Fact]
    public async Task AMultiplierThatIsNotAPositiveNumberIsRejected()
    {
        var failure = await Should.ThrowAsync<InvalidOperationException>(() => ClientAsync(multiplier: "0"));

        failure.Message.ShouldContain("Lamina:TimeoutMultiplier");
    }

    private static async Task<HttpClient> ClientAsync(string? multiplier)
    {
        await using var run = await HttpSuite.StartAsync(lamina =>
        {
            lamina.Configuration["Lamina:TimeoutMultiplier"] = multiplier;
            lamina.AddHttpClient("web-bff");
        });
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        return scope.Services.GetRequiredService<SutHttpClients>().For("web-bff");
    }
}
