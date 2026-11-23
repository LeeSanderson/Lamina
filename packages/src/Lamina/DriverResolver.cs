using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Lamina;

internal static class DriverResolver
{
    [SuppressMessage("Critical Code Smell", "S4018:Generic methods should provide type parameters", Justification = "A service lookup: TCapability is what's asked for, and the caller always states it.")]
    internal static TCapability For<TCapability>(IServiceProvider services, IReadOnlyCollection<string> driverChannels)
        where TCapability : class
    {
        var scope = services.GetRequiredService<SpecScope>();
        if (services.GetKeyedService<TCapability>(scope.Channel) is { } driver)
        {
            return driver;
        }

        var defaultChannel = services.GetRequiredService<ChannelSet>().Default;
        var fallback = services.GetKeyedService<TCapability>(defaultChannel) ?? throw new InvalidOperationException(
            $"{typeof(TCapability).Name} has no driver for '{scope.Channel}' or for the default channel '{defaultChannel}'. Drivers: {string.Join(", ", driverChannels)}.");
        scope.NoteFallback($"{typeof(TCapability).Name} has no '{scope.Channel}' driver, so it ran on '{defaultChannel}'");
        return fallback;
    }
}
