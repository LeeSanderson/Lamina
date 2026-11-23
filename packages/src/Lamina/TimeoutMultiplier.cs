using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Lamina;

/// <summary>
/// Reads <c>Lamina:TimeoutMultiplier</c> (default 1), which scales every wait and timeout Lamina's packages allow, for slow environments.
/// </summary>
public static class TimeoutMultiplier
{
    private const string Key = "Lamina:TimeoutMultiplier";

    /// <summary>
    /// Returns the configured multiplier, or 1 when it isn't set. Throws when it isn't a positive number.
    /// </summary>
    public static double Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var configured = configuration[Key];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return 1;
        }

        return double.TryParse(configured, NumberStyles.Float, CultureInfo.InvariantCulture, out var multiplier) && multiplier > 0 && double.IsFinite(multiplier)
            ? multiplier
            : throw new InvalidOperationException($"{Key} is '{configured}', but it must be a positive number, such as 2 or 1.5.");
    }
}
