namespace Lamina.Drivers.Http.Tests;

internal sealed class HttpSuite(Action<LaminaBuilder> configure) : LaminaSuite
{
    public static Task<LaminaRun> StartAsync(Action<LaminaBuilder> configure) =>
        new HttpSuite(configure).StartAsync(TestContext.Current.CancellationToken);

    protected override void Configure(LaminaBuilder builder)
    {
        builder
            .UseSut(sut => sut.AddProvider("fake", _ => new FakeSut()))
            .AddChannel("api", isDefault: true);
        configure(builder);
    }
}
