using Folio.Customers;
using Folio.ServiceDefaults;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<CustomersDbContext>("customers-db");

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapCustomerEndpoints();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<CustomersDbContext>().Database.MigrateAsync();
}

await app.RunAsync();
