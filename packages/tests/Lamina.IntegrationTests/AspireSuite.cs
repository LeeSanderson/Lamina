using Lamina.Sut.Aspire;

namespace Lamina.IntegrationTests;

internal sealed class AspireSuite(params string[] appHostArgs) : LaminaSuite
{
    public string Settings { get; init; } = AppContext.BaseDirectory;

    protected override string SettingsDirectory => Settings;

    protected override void Configure(LaminaBuilder builder) =>
        builder
            .UseSut(sut => sut.Aspire<Projects.Lamina_IntegrationTests_AppHost>(appHostArgs))
            .AddChannel("api", isDefault: true);
}
