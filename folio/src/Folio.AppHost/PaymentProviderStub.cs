namespace Folio.AppHost;

internal static class PaymentProviderStub
{
    private const string Image = "sheyenrath/wiremock.net-alpine";

    // Pinned so start-up time and stub behaviour can't change without a code change. Image tags lag
    // the WireMock.Net NuGet releases and skip some (#19), so check Docker Hub before bumping.
    private const string ImageTag = "2.30.0";
    private const int HostPort = 5185;
    private const int ContainerPort = 80;

    internal static IResourceBuilder<ContainerResource> AddPaymentProviderStub(this IDistributedApplicationBuilder builder, string name) =>
        builder.AddContainer(name, Image, ImageTag)
            .WithHttpEndpoint(port: HostPort, targetPort: ContainerPort, name: "http")
            .WithHttpHealthCheck("/__admin/health");
}
