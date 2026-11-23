namespace Lamina.XunitV3.Tests.Samples;

[Channels("web")]
public abstract class FallbackSpecs : SampleSpec
{
    [Fact]
    public void ACapabilityWithNoWebDriverUsesTheDefaultChannelsDriver() => Assert.Equal(nameof(ApiShelfDriver), Shelf.DriverName);
}
