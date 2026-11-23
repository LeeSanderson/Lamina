namespace Lamina.Dsl.Tests;

internal sealed class DslSuite : LaminaSuite
{
    public static Task<LaminaRun> StartAsync() => new DslSuite().StartAsync(TestContext.Current.CancellationToken);

    protected override void Configure(LaminaBuilder builder) =>
        builder
            .UseSut(sut => sut.AddProvider("fake", _ => new NoSut()))
            .AddChannel("api", isDefault: true)
            .AddDslsFromAssemblyOf<CatalogueDsl>()
            .Services.AddSingleton<ICatalogue, InMemoryCatalogue>();
}
