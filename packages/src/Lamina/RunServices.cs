using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lamina;

// The services every run provides on top of the suite's own: configuration, the SUT, channels, the spec scope and Eventually.
internal static class RunServices
{
    internal static void Add(IServiceCollection services, IConfiguration configuration, SutAccessor accessor, ChannelSet channels)
    {
        services.AddSingleton(_ => configuration);
        services.AddSingleton(accessor);
        services.AddSingleton(channels);
        services.AddSingleton<ISystemUnderTest>(sp => new BorrowedSut(sp.GetRequiredService<SutAccessor>().Sut));
        services.AddScoped<SpecScopeHolder>();
        services.AddScoped(sp => sp.GetRequiredService<SpecScopeHolder>().Scope);
        services.TryAddSingleton(TimeProvider.System);
        var multiplier = TimeoutMultiplier.Read(configuration);
        services.AddScoped(sp => new Eventually(sp.GetRequiredService<SpecScope>(), sp.GetRequiredService<TimeProvider>(), multiplier));
    }
}
