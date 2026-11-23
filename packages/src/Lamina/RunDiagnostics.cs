using System.Globalization;

namespace Lamina;

internal static class RunDiagnostics
{
    internal static string StartLine(string provider, ISystemUnderTest sut, TimeSpan timeToReady)
    {
        var endpoints = sut.EndpointNames.Select(name => $"{name} = {sut.Endpoint(name)}");
        var metadata = sut.Metadata.Select(entry => $"{entry.Key} = {entry.Value}");
        return $"Lamina run started with SUT provider '{provider}', ready in {Seconds(timeToReady)}. " +
            $"Endpoints: {ListOrNone(endpoints)}. Metadata: {ListOrNone(metadata)}.";
    }

    internal static string TeardownLine(TimeSpan teardown) => $"Lamina run torn down in {Seconds(teardown)}.";

    private static string Seconds(TimeSpan duration) => $"{duration.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s";

    private static string ListOrNone(IEnumerable<string> items) => string.Join(", ", items) is { Length: > 0 } list ? list : "none";
}
