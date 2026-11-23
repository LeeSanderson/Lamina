using Lamina.Stubs.WireMock;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddContainer("payment-provider", WireMockImage.Image)
    .WithImageTag(WireMockImage.Tag)
    .WithHttpEndpoint(targetPort: 80, name: "http")
    .WithHttpHealthCheck("/__admin/health");

builder.AddProject<Projects.Lamina_IntegrationTests_Service>("greeter")
    .WithEnvironment("Greeting", builder.Configuration["Greeting"] ?? "hello")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Lamina_IntegrationTests_Service>("late")
    .WithEnvironment("HealthyAfter", builder.Configuration["LateBy"] ?? "00:00:03")
    .WithHttpHealthCheck("/health");

await builder.Build().RunAsync().ConfigureAwait(false);
