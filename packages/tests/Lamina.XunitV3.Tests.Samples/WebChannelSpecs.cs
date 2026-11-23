namespace Lamina.XunitV3.Tests.Samples;

[Trait("Channel", "web")]
public sealed class WebChannelSpecs : SampleSpec
{
    [Fact]
    public void ASpecWithAChannelTraitRunsOnThatChannel() => Assert.Equal("web", Scope.Channel);
}
