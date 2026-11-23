using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lamina.Drivers.Http;

/// <summary>
/// Registers <see cref="HttpClient"/>s named by SUT endpoint, and the test JWT minter.
/// </summary>
public static class LaminaBuilderHttpExtensions
{
    /// <summary>
    /// Registers a client for the SUT endpoint <paramref name="endpoint"/>, such as <c>web-bff</c>. Get it from a spec's services
    /// with <see cref="SutHttpClients.For"/>. The returned builder adds handlers to its pipeline.
    /// </summary>
    public static IHttpClientBuilder AddHttpClient(this LaminaBuilder builder, string endpoint)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(endpoint);
        builder.Services.TryAddScoped(sp => new SutHttpClients(
            sp.GetRequiredService<SpecScope>(),
            sp.GetRequiredService<ISystemUnderTest>(),
            sp.GetRequiredService<IHttpMessageHandlerFactory>(),
            sp.GetServices<HttpEndpoint>(),
            sp.GetRequiredService<IConfiguration>()));
        builder.Services.AddSingleton(new HttpEndpoint(endpoint));
        return builder.Services.AddHttpClient(endpoint);
    }

    /// <summary>
    /// Registers <typeparamref name="TClient"/> as a scoped typed client for the SUT endpoint <paramref name="endpoint"/>. Its
    /// constructor takes the endpoint's <see cref="HttpClient"/>, plus any other services it needs.
    /// </summary>
    [SuppressMessage("Critical Code Smell", "S4018:Generic methods should provide type parameters", Justification = "Mirrors IHttpClientFactory's AddHttpClient<TClient>: the caller always states the client type.")]
    public static IHttpClientBuilder AddHttpClient<TClient>(this LaminaBuilder builder, string endpoint)
        where TClient : class
    {
        var client = builder.AddHttpClient(endpoint);
        builder.Services.AddScoped(sp => ActivatorUtilities.CreateInstance<TClient>(sp, sp.GetRequiredService<SutHttpClients>().For(endpoint)));
        return client;
    }

    /// <summary>
    /// Registers <see cref="TestJwt"/>, which mints tokens signed with the suite's test key.
    /// </summary>
    public static LaminaBuilder AddTestJwt(this LaminaBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddSingleton<TestJwt>();
        return builder;
    }
}
