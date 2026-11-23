using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lamina;

/// <summary>
/// Declares a suite's channels, system under test and services. Extension packages add to <see cref="Services"/>.
/// </summary>
public sealed class LaminaBuilder
{
    private readonly IConfigurationRoot _configuration;
    private readonly SutBuilder _sut;
    private readonly List<string> _channels = [];
    private string? _defaultChannel;

    internal LaminaBuilder(IConfigurationRoot configuration)
    {
        _configuration = configuration;
        _sut = new SutBuilder(configuration);
    }

    public IServiceCollection Services { get; } = new ServiceCollection();

    public IConfiguration Configuration => _configuration;

    public LaminaBuilder UseSut(Action<SutBuilder> configure)
    {
        configure(_sut);
        return this;
    }

    public LaminaBuilder AddChannel(string name) => AddChannel(name, isDefault: false);

    public LaminaBuilder AddChannel(string name, bool isDefault)
    {
        if (_channels.Contains(name, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Channel '{name}' is already added.");
        }

        if (isDefault && _defaultChannel is not null)
        {
            throw new InvalidOperationException($"Channel '{_defaultChannel}' is already the default, so '{name}' can't be.");
        }

        _channels.Add(name);
        if (isDefault)
        {
            _defaultChannel = name;
        }

        return this;
    }

    internal async Task<LaminaRun> StartAsync(CancellationToken cancellationToken)
    {
        var channels = new ChannelSet(
            _channels,
            _defaultChannel ?? throw new InvalidOperationException("No default channel. Call AddChannel(name, isDefault: true) in the suite's Configure."));
        var accessor = new SutAccessor();
        Services.AddSingleton<IConfiguration>(_ => _configuration);
        Services.AddSingleton(accessor);
        Services.AddSingleton<ISystemUnderTest>(sp => new BorrowedSut(sp.GetRequiredService<SutAccessor>().Sut));
        Services.AddScoped<SpecScopeHolder>();
        Services.AddScoped(sp => sp.GetRequiredService<SpecScopeHolder>().Scope);

        var services = Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        ISystemUnderTest? system = null;
        try
        {
            var started = TimeProvider.System.GetTimestamp();
            var (providerName, provider) = _sut.Select(services);
            system = await provider.StartAsync(cancellationToken).ConfigureAwait(false);
            accessor.Sut = system;
            await system.WaitUntilReadyAsync(cancellationToken).ConfigureAwait(false);
            var startLine = RunDiagnostics.StartLine(providerName, system, TimeProvider.System.GetElapsedTime(started));
            foreach (var hook in services.GetServices<IRunStartHook>())
            {
                await hook.OnRunStartAsync(system, cancellationToken).ConfigureAwait(false);
            }

            return new LaminaRun(services, system, channels, new TestIdMinter(), startLine);
        }
        catch
        {
            await services.DisposeAsync().ConfigureAwait(false);
            if (system is not null)
            {
                await system.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }
    }
}
