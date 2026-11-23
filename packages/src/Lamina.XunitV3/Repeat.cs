using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Lamina.XunitV3;

/// <summary>
/// Reads <c>Lamina:Repeat</c>: how many copies of each spec discovery makes. Missing or 1 means one copy, under its normal name.
/// </summary>
internal static class Repeat
{
    private const string Key = "Lamina:Repeat";

    internal static int Read(IConfiguration configuration)
    {
        var configured = configuration[Key];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return 1;
        }

        return int.TryParse(configured, NumberStyles.None, CultureInfo.InvariantCulture, out var copies) && copies > 0
            ? copies
            : throw new InvalidOperationException($"{Key} is '{configured}', but it must be a whole number of copies, 1 or more.");
    }
}
