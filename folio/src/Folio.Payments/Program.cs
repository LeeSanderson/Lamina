using Folio.Payments;
using Folio.ServiceDefaults;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<PaymentsDbContext>("payments-db");
builder.Services.AddSingleton(TimeProvider.System);
builder.AddPaymentProviderClient();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapPaymentEndpoints();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().Database.MigrateAsync();
}

await app.RunAsync();
