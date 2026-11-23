using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Lamina.Drivers.Http;

internal static class TimeoutMultiplier
{
    private const string Key = "Lamina:TimeoutMultiplier";

    internal static double Read(IConfiguration configuration)
    {
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
