// PROTOTYPE: deliberately broken specs (phase 1 acceptance criterion 2). Excluded from the normal run by trait:
//   dotnet run -- -trait- Broken=true        the real suite, all green
//   dotnet run -- -trait Broken=true         only these, all red, to read the failure messages
using Lamina.XunitV3;
using Xunit;
using static Folio.Commerce.Acceptance.Dsl.FolioChannels;

namespace Folio.Commerce.Acceptance.Specs;

[Channels(Api)]
[Trait("Broken", "true")]
public abstract class BrokenSpecs : FolioSpec
{
    // Eventually, predicate shape, times out: nobody sends the webhook.
    [Fact]
    public async Task B1_EventuallyTimesOutOnPredicate()
    {
        await Catalogue.AddBook(title: "Dune", stock: 1);
        await Shopping.PlaceOrder(title: "Dune", card: "visa");

        await Shopping.ConfirmOrderConfirmed();
    }

    // A plain xUnit assertion in a DSL, not Eventually: does it still get test ID and aliases?
    [Fact]
    public async Task B2_PlainAssertionInDsl()
    {
        await Catalogue.AddBook(title: "Dune", stock: 1);
        await Catalogue.ConfirmStock(title: "Dune", stock: 5);
    }

    // Eventually where the probe throws at first (BFF answers 503), then sees Pending: shows the observation history.
    [Fact]
    public async Task B3_ProbeThrowsThenObservesWrongState()
    {
        await Catalogue.AddBook(title: "Dune", stock: 1);
        await Shopping.AsCustomer("Outage-Olga").PlaceOrder(title: "Dune", card: "visa");

        await Shopping.ConfirmOrderConfirmed();
    }

    // Eventually, assertion-lambda shape, times out: the connection is never dropped, so there is no retry.
    [Fact]
    public async Task B4_EventuallyTimesOutOnAssertion()
    {
        await Catalogue.AddBook(title: "Dune");
        await Shopping.PlaceOrder(title: "Dune", card: "visa");

        await PaymentProvider.ConfirmRetriedWithSameIdempotencyKey(card: "visa");
    }

    // Not an assertion at all: a DSL misuse (nothing remembered for "Emma").
    [Fact]
    public async Task B5_NonAssertionException()
    {
        await Catalogue.AddBook(title: "Dune");
        await Shopping.PlaceOrder(title: "Emma", card: "visa");
    }
}
