using Lamina.Dsl;
using Lamina.XunitV3;
using Lamina.XunitV3.Tests.Samples;

[assembly: AssemblyFixture(typeof(LaminaFixture<SampleSuite>))]
[assembly: TestFramework(typeof(LaminaTestFramework<SampleSuite>))]

namespace Lamina.XunitV3.Tests.Samples;

public sealed class SampleSuite : LaminaSuite
{
    protected override void Configure(LaminaBuilder builder) =>
        builder
            .UseSut(sut => sut.AddProvider("fake", _ => new FakeSut()))
            .AddChannel("api", isDefault: true)
            .AddChannel("web")
            .AddDriver<IShelfDriver, ApiShelfDriver>("api")
            .AddDslsFromAssemblyOf<LibraryDsl>();
}
