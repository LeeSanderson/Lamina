using Lamina.Stubs.WireMock;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddContainer("payment-provider", WireMockImage.Image)
    .WithImageTag(WireMockImage.Tag)
    .WithHttpEndpoint(targetPort: 80, name: "http")
    .WithHttpHealthCheck("/__admin/health");

await builder.Build().RunAsync().ConfigureAwait(false);
