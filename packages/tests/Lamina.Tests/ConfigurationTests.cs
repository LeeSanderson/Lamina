using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lamina.Tests;

public sealed class ConfigurationTests : IDisposable
{
    private readonly string _section = $"Probe{Guid.NewGuid():N}";
    private readonly DirectoryInfo _settings = Directory.CreateTempSubdirectory("lamina-config-");

    [Fact]
    public async Task LocalSettingsOverrideAppSettingsAndEnvironmentVariablesOverrideBoth()
    {
        Write("appsettings.json", json: "json", local: "json", environment: "json");
        Write("appsettings.local.json", json: null, local: "local", environment: "local");
        Environment.SetEnvironmentVariable($"{_section}__Environment", "environment");

        await using var run = await new TestSuite { Settings = _settings.FullName }.StartAsync(TestContext.Current.CancellationToken);
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var configuration = scope.Services.GetRequiredService<IConfiguration>().GetSection(_section);

        configuration["Json"].ShouldBe("json");
        configuration["Local"].ShouldBe("local");
        configuration["Environment"].ShouldBe("environment");
    }

    [Fact]
    public async Task NeitherSettingsFileIsRequired()
    {
        await using var run = await new TestSuite { Settings = _settings.FullName }.StartAsync(TestContext.Current.CancellationToken);

        run.DefaultChannel.ShouldBe("api");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable($"{_section}__Environment", value: null);
        _settings.Delete(recursive: true);
    }

    private void Write(string file, string? json, string local, string environment)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal) { ["Local"] = local, ["Environment"] = environment };
        if (json is not null)
        {
            values["Json"] = json;
        }

        var body = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal) { [_section] = values });
        File.WriteAllText(Path.Combine(_settings.FullName, file), body);
    }
}
