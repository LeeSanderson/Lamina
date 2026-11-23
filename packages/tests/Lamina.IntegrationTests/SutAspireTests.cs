using System.Net;

namespace Lamina.IntegrationTests;

public sealed class SutAspireTests : IDisposable
{
    private readonly HttpClient _http = new();

    [Fact]
    public async Task StartsTheAppHostWithTheGivenArgumentsAndServesEachResourceOnItsHttpEndpoint()
    {
        await using var run = await new AspireSuite("--Greeting", "bonjour").StartAsync(TestContext.Current.CancellationToken);

        var greeting = await _http.GetStringAsync(run.Sut.Endpoint("greeter"), TestContext.Current.CancellationToken);

        greeting.ShouldBe("bonjour");
    }

    [Fact]
    public async Task TheRunStartsOnlyOnceEveryResourceIsHealthy()
    {
        await using var run = await new AspireSuite("--LateBy", "00:00:04").StartAsync(TestContext.Current.CancellationToken);

        using var health = await _http.GetAsync(new Uri(run.Sut.Endpoint("late"), "health"), TestContext.Current.CancellationToken);

        health.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DisposingTheRunStopsTheAppHost()
    {
        var run = await new AspireSuite().StartAsync(TestContext.Current.CancellationToken);
        var greeter = run.Sut.Endpoint("greeter");

        await run.DisposeAsync();

        await Should.ThrowAsync<HttpRequestException>(() => _http.GetStringAsync(greeter, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnEndpointThatIsNotAResourceFailsListingTheResources()
    {
        await using var run = await new AspireSuite().StartAsync(TestContext.Current.CancellationToken);

        var error = Should.Throw<ArgumentException>(() => run.Sut.Endpoint("greeterr"));

        error.Message.ShouldStartWith("The AppHost has no resource named 'greeterr'. Resources: greeter, late.");
    }

    [Fact]
    public async Task MetadataNamesTheProviderTheAppHostAndItsArguments()
    {
        await using var run = await new AspireSuite("--Greeting", "hi").StartAsync(TestContext.Current.CancellationToken);

        run.Sut.Metadata.ShouldBe(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["provider"] = "aspire",
                ["apphost"] = "Lamina.IntegrationTests.AppHost",
                ["arguments"] = "--Greeting hi",
                ["resources"] = "greeter, late",
            },
            ignoreOrder: true);
    }

    [Fact]
    public async Task AResourceThatNeverBecomesHealthyFailsTheRunNamingItsStateAndLogs()
    {
        var settings = Directory.CreateTempSubdirectory("lamina-aspire-");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(settings.FullName, "appsettings.json"), """{ "Lamina": { "Sut": { "ReadyTimeout": "00:00:15" } } }""", TestContext.Current.CancellationToken);
            var suite = new AspireSuite("--LateBy", "1.00:00:00") { Settings = settings.FullName };

            var error = await Should.ThrowAsync<TimeoutException>(() => suite.StartAsync(TestContext.Current.CancellationToken));

            error.Message.ShouldStartWith("The Aspire AppHost was not ready within 00:00:15. Resources not healthy:");
            error.Message.ShouldContain("- late: state Running, health Unhealthy");
            error.Message.ShouldNotContain("- greeter:");
            error.Message.ShouldContain("Now listening on");
        }
        finally
        {
            settings.Delete(recursive: true);
        }
    }

    public void Dispose() => _http.Dispose();
}
