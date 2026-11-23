namespace Lamina.XunitV3.Tests.Samples;

[Channels("api", "web")]
public abstract class ChannelledSpecs : SampleSpec
{
    [Fact]
    public void AGeneratedClassRunsOnTheChannelInItsTrait() =>
        Assert.Equal(TestContext.Current.Test!.Traits["Channel"].Single(), Scope.Channel);
}
