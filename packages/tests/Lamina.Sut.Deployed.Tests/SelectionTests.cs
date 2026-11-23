namespace Lamina.Sut.Deployed.Tests;

public sealed class SelectionTests
{
    [Fact]
    public async Task OrDeployedIsChosenAlongsideAnotherProviderWhenConfigurationNamesIt()
    {
        await using var run = await DeployedSuite.StartAsync(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Lamina:Sut:Provider"] = "deployed",
                ["Lamina:Sut:Deployed:Endpoints:web-bff"] = "http://localhost:5180",
            },
            sut => sut.AddProvider("aspire", _ => throw new InvalidOperationException("aspire should not start")).OrDeployed());

        run.Sut.Metadata["provider"].ShouldBe("deployed");
    }
}
