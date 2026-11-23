namespace Lamina.Stubs.WireMock;

/// <summary>
/// The WireMock.Net container image Lamina's admin client is checked against. Pin an AppHost's stub to it with
/// <c>.WithImageTag(WireMockImage.Tag)</c>: the default image is an unpinned <c>latest</c> that lags the NuGet release.
/// </summary>
public static class WireMockImage
{
    public static string Image => "sheyenrath/wiremock.net-alpine";

    public static string Tag => "2.31.0";
}
