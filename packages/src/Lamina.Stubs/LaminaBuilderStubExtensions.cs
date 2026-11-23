using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lamina.Stubs;

public static class LaminaBuilderStubExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TStub"/> as scoped, one per spec, requires the SUT to have the endpoint it stands in for,
    /// and installs its run defaults at run start.
    /// </summary>
    [SuppressMessage("Critical Code Smell", "S4018:Generic methods should provide type parameters", Justification = "TStub names the stub to register, as in AddScoped<T>(); the caller always states it.")]
    public static LaminaBuilder AddStub<TStub>(this LaminaBuilder builder)
        where TStub : LaminaStub
    {
        // A stub names its endpoint on the instance, so one is built, unattached, just to read it.
        builder.RequireEndpoint(services =>
        {
            using var scope = services.CreateScope();
            return ActivatorUtilities.CreateInstance<TStub>(scope.ServiceProvider).EndpointName;
        });
        builder.Services.TryAddSingleton<StubContext>();
        builder.Services.AddScoped(services =>
        {
            var stub = ActivatorUtilities.CreateInstance<TStub>(services);
            stub.Attach(services.GetRequiredService<SpecScope>(), services.GetRequiredService<StubContext>());
            return stub;
        });
        builder.Services.AddSingleton<IRunStartHook, StubDefaultsHook<TStub>>();
        return builder;
    }
}
