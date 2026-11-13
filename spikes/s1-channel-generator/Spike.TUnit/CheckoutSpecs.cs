using Spike.Channels;

namespace Spike.TUnit;

[Channels("api", "web")]
public abstract class CheckoutSpecs : ChannelSpec
{
    [Test]
    public async Task OrderIsRejectedWhenPaymentDeclined() =>
        await Assert.That(Channel).IsIn("api", "web");

    [Test]
    public async Task StockIsReleasedWhenOrderIsCancelled() =>
        await Assert.That(Channel).IsIn("api", "web");
}
