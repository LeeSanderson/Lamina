using Folio.AppHost;

const string healthPath = "/health";

var builder = DistributedApplication.CreateBuilder(args);
FolioProfile.EnsureSupported(builder.Configuration);

// Four services share this server, and each pools up to 100 connections. Postgres's default of 100 runs out when the
// acceptance suite runs 50 copies of each spec at once ("53300: sorry, too many clients already").
var postgres = builder.AddPostgres("postgres")
    .WithArgs("-c", "max_connections=500");
var catalogueDb = postgres.AddDatabase("catalogue-db");
var customersDb = postgres.AddDatabase("customers-db");
var orderingDb = postgres.AddDatabase("ordering-db");
var paymentsDb = postgres.AddDatabase("payments-db");

var paymentProvider = builder.AddPaymentProviderStub("payment-provider");

var catalogue = builder.AddProject<Projects.Folio_Catalogue>("catalogue")
    .WithReference(catalogueDb)
    .WaitFor(catalogueDb)
    .WithHttpHealthCheck(healthPath);

var customers = builder.AddProject<Projects.Folio_Customers>("customers")
    .WithReference(customersDb)
    .WaitFor(customersDb)
    .WithHttpHealthCheck(healthPath);

var payments = builder.AddProject<Projects.Folio_Payments>("payments")
    .WithReference(paymentsDb)
    .WaitFor(paymentsDb)
    .WithReference(paymentProvider.GetEndpoint("http"))
    .WaitFor(paymentProvider)
    .WithHttpHealthCheck(healthPath);

var ordering = builder.AddProject<Projects.Folio_Ordering>("ordering")
    .WithReference(orderingDb)
    .WaitFor(orderingDb)
    .WithReference(customers)
    .WaitFor(customers)
    .WithReference(catalogue)
    .WaitFor(catalogue)
    .WithReference(payments)
    .WaitFor(payments)
    .WithHttpHealthCheck(healthPath);

// Payments sends payment outcomes to Ordering, and Ordering waits for Payments, so this reference has no WaitFor.
payments.WithReference(ordering);

builder.AddProject<Projects.Folio_WebBff>("web-bff")
    .WithReference(catalogue)
    .WaitFor(catalogue)
    .WithReference(ordering)
    .WaitFor(ordering)
    .WithEnvironment("Authentication__TestSigningKey", builder.Configuration["Acceptance:TestSigningKey"])
    .WithEnvironment("Authentication__TestIssuer", builder.Configuration["Acceptance:TestIssuer"])
    .WithEnvironment("Authentication__TestAudience", builder.Configuration["Acceptance:TestAudience"])
    .WithHttpHealthCheck(healthPath);

await builder.Build().RunAsync();
