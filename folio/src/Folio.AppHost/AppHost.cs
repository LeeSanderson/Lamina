using Folio.AppHost;

var builder = DistributedApplication.CreateBuilder(args);
FolioProfile.EnsureSupported(builder.Configuration);

var postgres = builder.AddPostgres("postgres");
var catalogueDb = postgres.AddDatabase("catalogue-db");

var catalogue = builder.AddProject<Projects.Folio_Catalogue>("catalogue")
    .WithReference(catalogueDb)
    .WaitFor(catalogueDb)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Folio_WebBff>("web-bff")
    .WithReference(catalogue)
    .WaitFor(catalogue)
    .WithEnvironment("Authentication__TestSigningKey", builder.Configuration["Acceptance:TestSigningKey"])
    .WithEnvironment("Authentication__TestIssuer", builder.Configuration["Acceptance:TestIssuer"])
    .WithEnvironment("Authentication__TestAudience", builder.Configuration["Acceptance:TestAudience"])
    .WithHttpHealthCheck("/health");

await builder.Build().RunAsync();
