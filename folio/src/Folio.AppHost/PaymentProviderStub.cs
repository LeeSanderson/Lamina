namespace Folio.AppHost;

internal static class PaymentProviderStub
{
    private const string Image = "sheyenrath/wiremock.net-alpine";

    // Pinned so start-up time and stub behaviour can't change without a code change. Keep it in step with
    // Lamina.Stubs.WireMock's WireMockImage.Tag, the image Lamina's admin client is checked against.
    private const string ImageTag = "2.31.0";
    private const int HostPort = 5185;
    private const int ContainerPort = 80;

    internal static IResourceBuilder<ContainerResource> AddPaymentProviderStub(this IDistributedApplicationBuilder builder, string name) =>
        builder.AddContainer(name, Image, ImageTag)
            .WithHttpEndpoint(port: HostPort, targetPort: ContainerPort, name: "http")
            .WithHttpHealthCheck("/__admin/health");
}
