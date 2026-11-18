// The suite's composition root. Lives in Folio.Commerce.Acceptance.Specs: like Program.cs, it is the one place
// allowed to name drivers (the layering analyzer would exempt LaminaSuite subclasses).
using Folio.Commerce.Acceptance.Drivers;
using Folio.Commerce.Acceptance.Dsl;
using Folio.Commerce.Acceptance.FakeSut;
using Folio.Commerce.Acceptance.Specs;
using Lamina;
using Lamina.XunitV3;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[assembly: AssemblyFixture(typeof(LaminaFixture<FolioSuite>))]
[assembly: CaptureConsole]
namespace Folio.Commerce.Acceptance.Specs;

public sealed class FolioSuite : LaminaSuite
{
    private readonly InMemoryFolio folio = new();

    // Real phase 1: .UseSut(sut => sut.Aspire<Projects.Folio_AppHost>().OrDeployed(section: "Lamina:Sut")).
    protected override void Configure(LaminaBuilder b) => b
        .UseSut(() => folio)
        .AddServices(s => s.AddSingleton(folio))
        .AddChannel(FolioChannels.Api, isDefault: true)
        .AddChannel("web")
        .AddDriver<ICatalogueDriver, ApiCatalogueDriver>(FolioChannels.Api)
        .AddDriver<IShoppingDriver, ApiShoppingDriver>(FolioChannels.Api)
        .AddStub<PaymentProviderStub>()
        .AddDslsFromAssemblyOf<ShoppingDsl>();
}
