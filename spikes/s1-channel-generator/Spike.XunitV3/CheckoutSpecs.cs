using Spike.Channels;
using Xunit;

namespace Spike.XunitV3;

[Channels("api", "web")]
public abstract class CheckoutSpecs : ChannelSpec
{
    private static readonly string[] KnownChannels = ["api", "web"];

    [Fact]
    public void OrderIsRejectedWhenPaymentDeclined() =>
        Assert.Contains(Channel, KnownChannels);

    [Fact]
    public void StockIsReleasedWhenOrderIsCancelled() =>
        Assert.Contains(Channel, KnownChannels);
}
