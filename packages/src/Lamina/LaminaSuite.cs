using Microsoft.Extensions.Configuration;

namespace Lamina;

/// <summary>
/// A suite's composition root. Override <see cref="Configure"/> to declare channels, the SUT, drivers and DSLs.
/// </summary>
public abstract class LaminaSuite
{
    /// <summary>
    /// Gets the folder that holds <c>appsettings.json</c> and <c>appsettings.local.json</c>.
    /// </summary>
    protected virtual string SettingsDirectory => AppContext.BaseDirectory;

    /// <summary>
    /// Builds the run's services, starts the system under test, waits for it and runs every run-start hook.
    /// </summary>
    public async Task<LaminaRun> StartAsync(CancellationToken cancellationToken)
    {
        var builder = new LaminaBuilder(ReadConfiguration());
        Configure(builder);
        return await builder.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the suite's configuration: <c>appsettings.json</c>, then <c>appsettings.local.json</c>, then environment variables.
    /// Run settings read before the run starts, such as <c>Lamina:Repeat</c> at discovery, come from here too.
    /// </summary>
    public IConfigurationRoot ReadConfiguration() =>
        new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(SettingsDirectory, "appsettings.json"), optional: true, reloadOnChange: false)
            .AddJsonFile(Path.Combine(SettingsDirectory, "appsettings.local.json"), optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

    protected abstract void Configure(LaminaBuilder builder);
}
