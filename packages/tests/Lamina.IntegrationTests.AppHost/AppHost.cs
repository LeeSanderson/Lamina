using Lamina.Stubs.WireMock;

const string service = "../Lamina.IntegrationTests.Service";

var builder = DistributedApplication.CreateBuilder(args);

builder.AddContainer("payment-provider", WireMockImage.Image)
    .WithImageTag(WireMockImage.Tag)
    .WithHttpEndpoint(targetPort: 80, name: "http")
    .WithHttpHealthCheck("/__admin/health");

builder.AddDotnetProject("greeter", service)
    .WithEnvironment("Greeting", builder.Configuration["Greeting"] ?? "hello")
    .WithHttpHealthCheck("/health");

builder.AddDotnetProject("late", service)
    .WithEnvironment("HealthyAfter", builder.Configuration["LateBy"] ?? "00:00:03")
    .WithHttpHealthCheck("/health");

await builder.Build().RunAsync().ConfigureAwait(false);
