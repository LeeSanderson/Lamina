namespace Folio.Commerce.Acceptance.Specs;

// Fails on purpose, to show what a failing spec reports (phase 1 acceptance criterion 2). Explicit, so a normal run leaves it
// out; run it on its own with: dotnet run -- -trait Broken=true -explicit only
[Trait("Broken", "true")]
public sealed class BrokenSpecs : FolioSpec
{
    [Fact(Explicit = true)]
    public async Task StockNeverReachesWhatTheSpecExpects()
    {
        await Shopping.AsStaff("Sam");
        await Catalogue.AddBook(ABook("Dune").WithStock(1));

        await Catalogue.ConfirmInStock("Dune", 5);
    }
}
