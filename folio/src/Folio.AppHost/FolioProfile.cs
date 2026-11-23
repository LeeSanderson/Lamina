using Microsoft.Extensions.Configuration;

namespace Folio.AppHost;

internal static class FolioProfile
{
    private const string Acceptance = "acceptance";

    internal static void EnsureSupported(IConfiguration configuration)
    {
        var profile = configuration["Profile"];
        if (string.IsNullOrWhiteSpace(profile))
        {
            throw new InvalidOperationException(
                $"The Folio AppHost needs a Profile setting. Pass '--Profile {Acceptance}', or use the '{Acceptance}' launch profile. The only supported profile is '{Acceptance}'.");
        }

        if (!string.Equals(profile, Acceptance, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The Folio AppHost doesn't support the profile '{profile}'. The only supported profile is '{Acceptance}'.");
        }
    }
}
