namespace Lamina.Sut.Deployed.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public async Task EndpointsAreReadFromConfiguration()
    {
        await using var run = await DeployedSuite.StartAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Lamina:Sut:Deployed:Endpoints:web-bff"] = "http://localhost:5180",
            ["Lamina:Sut:Deployed:Endpoints:payments"] = "http://localhost:5183/",
        });

        run.Sut.Endpoint("web-bff").ShouldBe(new Uri("http://localhost:5180"));
        run.Sut.Endpoint("payments").ShouldBe(new Uri("http://localhost:5183/"));
        run.Sut.EndpointNames.ShouldBe(["payments", "web-bff"], ignoreOrder: true);
    }

    [Fact]
    public async Task MetadataCopiesTheConfiguredMetadataAndNamesTheProvider()
    {
        await using var run = await DeployedSuite.StartAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Lamina:Sut:Deployed:Endpoints:web-bff"] = "http://localhost:5180",
            ["Lamina:Sut:Deployed:Metadata:environment"] = "staging",
            ["Lamina:Sut:Deployed:Metadata:build"] = "1.2.3",
        });

        run.Sut.Metadata.ShouldBe(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["provider"] = "deployed",
                ["environment"] = "staging",
                ["build"] = "1.2.3",
            },
            ignoreOrder: true);
    }

    [Fact]
    public async Task NoEndpointsFailsTheRunAtOnce()
    {
        var error = await Should.ThrowAsync<InvalidOperationException>(() => DeployedSuite.StartAsync(new Dictionary<string, string>(StringComparer.Ordinal)));

        error.Message.ShouldBe("Lamina:Sut:Provider is 'deployed', but Lamina:Sut:Deployed:Endpoints has no endpoints. Add a URL for each endpoint the suite uses, such as Lamina:Sut:Deployed:Endpoints:web-bff.");
    }

    [Fact]
    public async Task AReadinessPathForAMissingEndpointFailsTheRunAtOnce()
    {
        await using var neverReady = await FakeService.StartAsync(failuresBeforeReady: 1000);

        var error = await Should.ThrowAsync<InvalidOperationException>(() => DeployedSuite.StartAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Lamina:Sut:Deployed:Endpoints:web-bff"] = neverReady.BaseAddress.ToString(),
            ["Lamina:Sut:Deployed:Readiness:web-bff"] = "/health",
            ["Lamina:Sut:Deployed:Readiness:payments"] = "/health",
        }));

        error.Message.ShouldBe("Lamina:Sut:Deployed:Readiness:payments has no matching endpoint. Add Lamina:Sut:Deployed:Endpoints:payments. Known: web-bff.");
        neverReady.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("localhost:5180")]
    [InlineData("/api")]
    public async Task AnEndpointWithoutAnAbsoluteUrlFailsTheRunAtOnce(string configured)
    {
        var error = await Should.ThrowAsync<InvalidOperationException>(() => DeployedSuite.StartAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Lamina:Sut:Deployed:Endpoints:web-bff"] = configured,
        }));

        error.Message.ShouldBe($"Lamina:Sut:Deployed:Endpoints:web-bff is '{configured}', which is not an absolute http or https URL.");
    }

    [Fact]
    public async Task AnUnknownEndpointNameThrowsListingTheKnownNames()
    {
        await using var run = await DeployedSuite.StartAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Lamina:Sut:Deployed:Endpoints:web-bff"] = "http://localhost:5180",
            ["Lamina:Sut:Deployed:Endpoints:payments"] = "http://localhost:5183",
        });

        var error = Should.Throw<ArgumentException>(() => run.Sut.Endpoint("ordering"));

        error.Message.ShouldStartWith("Lamina:Sut:Deployed:Endpoints has no endpoint named 'ordering'. Known: payments, web-bff.");
    }
}
