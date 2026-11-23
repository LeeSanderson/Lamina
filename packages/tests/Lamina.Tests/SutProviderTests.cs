namespace Lamina.Tests;

public sealed class SutProviderTests : IDisposable
{
    private readonly DirectoryInfo _settings = Directory.CreateTempSubdirectory("lamina-sut-");

    [Fact]
    public async Task TheProviderNamedInConfigurationStartsTheSut()
    {
        await File.WriteAllTextAsync(Path.Combine(_settings.FullName, "appsettings.json"), """{ "Lamina": { "Sut": { "Provider": "second" } } }""", TestContext.Current.CancellationToken);
        var started = new List<string>();
        var suite = new TestSuite(b => b.UseSut(sut => sut.AddProvider("second", _ => new FakeSut(started)))) { Settings = _settings.FullName };

        await using var run = await suite.StartAsync(TestContext.Current.CancellationToken);

        started.ShouldContain("sut started");
        suite.Events.ShouldNotContain("sut started");
    }

    [Fact]
    public async Task AnUnregisteredProviderNameFailsTheRunListingTheRegisteredNames()
    {
        await File.WriteAllTextAsync(Path.Combine(_settings.FullName, "appsettings.json"), """{ "Lamina": { "Sut": { "Provider": "aspir" } } }""", TestContext.Current.CancellationToken);
        var suite = new TestSuite { Settings = _settings.FullName };

        var error = await Should.ThrowAsync<InvalidOperationException>(() => suite.StartAsync(TestContext.Current.CancellationToken));

        error.Message.ShouldBe("Lamina:Sut:Provider is 'aspir', which is not a registered SUT provider. Registered: fake.");
    }

    public void Dispose() => _settings.Delete(recursive: true);
}
