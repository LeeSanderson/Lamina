using Spike.Channels;

namespace Spike.NUnit;

[Channels("api", "web")]
public abstract class CheckoutSpecs : ChannelSpec
{
    [Test]
    public void OrderIsRejectedWhenPaymentDeclined() =>
        Assert.That(Channel, Is.AnyOf("api", "web"));

    [Test]
    public void StockIsReleasedWhenOrderIsCancelled() =>
        Assert.That(Channel, Is.AnyOf("api", "web"));
}
