using Microsoft.Extensions.Configuration;

namespace Folio.ServiceDefaults;

public static class ClientAddresses
{
    // The AppHost sets Clients:<service> to the base address of each service a Folio service calls.
    public static Uri ClientAddress(this IConfiguration configuration, string service) =>
        new(configuration.GetRequiredSection($"Clients:{service}").Value!);
}
