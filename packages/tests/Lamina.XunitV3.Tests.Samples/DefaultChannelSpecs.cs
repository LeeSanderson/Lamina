namespace Lamina.XunitV3.Tests.Samples;

public sealed class DefaultChannelSpecs : SampleSpec
{
    [Fact]
    public void ASpecWithNoChannelTraitRunsOnTheDefaultChannel() => Assert.Equal("api", Scope.Channel);
}
