using Folio.Commerce.Acceptance.Dsl;
using Lamina;
using Lamina.Drivers.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Folio.Commerce.Acceptance.Drivers;

public static class ApiDrivers
{
    internal const string WebBff = "web-bff";

    /// <summary>
    /// Registers the drivers that act through the Web BFF's API as <paramref name="channel"/>'s drivers, and the test JWT minter they sign in with.
    /// </summary>
    public static LaminaBuilder AddApiDrivers(this LaminaBuilder builder, string channel)
    {
        builder.AddTestJwt();
        builder.AddHttpClient<WebBffClient>(WebBff);
        builder.Services.AddScoped<ApiSession>();
        return builder
            .AddDriver<ICatalogueDriver, ApiCatalogueDriver>(channel)
            .AddDriver<IShoppingDriver, ApiShoppingDriver>(channel);
    }
}
