using Folio.Commerce.Acceptance.Dsl;
using Lamina;
using Lamina.Drivers.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Folio.Commerce.Acceptance.Drivers;

public static class ApiDrivers
{
    internal const string WebBff = "web-bff";

    /// <summary>
    /// Registers the drivers that act through the Web BFF's API, and the test JWT minter they sign in with.
    /// </summary>
    public static LaminaBuilder AddApiDrivers(this LaminaBuilder builder)
    {
        builder.AddTestJwt();
        builder.AddHttpClient<WebBffClient>(WebBff);
        builder.Services.AddScoped<ApiSession>();
        builder.Services.AddScoped<ICatalogueDriver, ApiCatalogueDriver>();
        builder.Services.AddScoped<IShoppingDriver, ApiShoppingDriver>();
        return builder;
    }
}
