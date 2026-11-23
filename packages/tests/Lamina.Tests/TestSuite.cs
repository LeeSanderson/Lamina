using Microsoft.Extensions.DependencyInjection;

namespace Lamina.Tests;

internal sealed class TestSuite(Action<LaminaBuilder> configure) : LaminaSuite
{
    public TestSuite()
        : this(_ => { })
    {
    }

    public List<string> Events { get; } = [];

    public string? Settings { get; init; }

    protected override string SettingsDirectory => Settings ?? base.SettingsDirectory;

    public static Task<LaminaRun> StartAsync() => new TestSuite().StartAsync(TestContext.Current.CancellationToken);

    public static Task<LaminaRun> StartAsync(Action<LaminaBuilder> configure) =>
        new TestSuite(configure).StartAsync(TestContext.Current.CancellationToken);

    protected override void Configure(LaminaBuilder builder)
    {
        builder
            .UseSut(sut => sut.AddProvider("fake", _ => new FakeSut(Events)))
            .AddChannel("api", isDefault: true)
            .Services.AddSingleton(Events);
        configure(builder);
    }
}
