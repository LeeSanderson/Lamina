namespace Lamina.XunitV3.Tests;

public sealed class ChannelRunTests(SamplesRun samples, ApiChannelRun apiChannelRun) : IClassFixture<SamplesRun>, IClassFixture<ApiChannelRun>
{
    [Theory]
    [InlineData("ChannelledSpecs_Api", "api")]
    [InlineData("ChannelledSpecs_Web", "web")]
    public void EachChannelOfAChannelsSpecIsDiscoveredAsItsOwnClassWithAChannelTrait(string className, string channel)
    {
        var result = samples.Single(className, "AGeneratedClassRunsOnTheChannelInItsTrait");

        result.Traits["Channel"].ShouldBe([channel]);
        result.Passed.ShouldBeTrue(result.FailureMessage);
    }

    [Fact]
    public void ASpecWhoseCapabilityFellBackToTheDefaultChannelSaysSoInItsFooter()
    {
        var result = samples.Single("FallbackSpecs_Web", "ACapabilityWithNoWebDriverUsesTheDefaultChannelsDriver");

        result.Passed.ShouldBeTrue(result.FailureMessage);
        result.Output.ShouldContain("on 'web' · aliases: none · fell back: IShelfDriver has no 'web' driver, so it ran on 'api'");
    }

    [Fact]
    public void AChannelTraitFilterRunsOnlyThatChannelsClasses()
    {
        var apiOnly = apiChannelRun.Results;

        apiOnly.ShouldNotBeEmpty();
        apiOnly.ShouldAllBe(r => r.TypeName.EndsWith("_Api", StringComparison.Ordinal));
    }
}
