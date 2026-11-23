using Folio.Commerce.Acceptance.Drivers;
using Folio.Commerce.Acceptance.Dsl;
using Folio.Commerce.Acceptance.Specs;
using Lamina;
using Lamina.Dsl;
using Lamina.Sut.Aspire;
using Lamina.Sut.Deployed;
using Lamina.XunitV3;

[assembly: AssemblyFixture(typeof(LaminaFixture<FolioSuite>))]

namespace Folio.Commerce.Acceptance.Specs;

public sealed class FolioSuite : LaminaSuite
{
    protected override void Configure(LaminaBuilder builder) =>
        builder
            .UseSut(sut => sut.Aspire<Projects.Folio_AppHost>("--Profile", "acceptance").OrDeployed())
            .AddChannel(FolioChannels.Api, isDefault: true)
            .AddApiDrivers(FolioChannels.Api)
            .AddDslsFromAssemblyOf<CatalogueDsl>();
}
