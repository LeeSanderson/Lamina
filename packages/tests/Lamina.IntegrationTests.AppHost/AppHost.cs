var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Lamina_IntegrationTests_Service>("greeter")
    .WithEnvironment("Greeting", builder.Configuration["Greeting"] ?? "hello")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Lamina_IntegrationTests_Service>("late")
    .WithEnvironment("HealthyAfter", builder.Configuration["LateBy"] ?? "00:00:03")
    .WithHttpHealthCheck("/health");

await builder.Build().RunAsync().ConfigureAwait(false);
