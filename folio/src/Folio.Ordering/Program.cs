using Folio.Ordering;
using Folio.ServiceDefaults;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<OrderingDbContext>("ordering-db");
builder.Services.AddSingleton(TimeProvider.System);
builder.AddServiceClients();
builder.Services.AddScoped<OrderBook>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapOrderEndpoints();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Database.MigrateAsync();
}

await app.RunAsync();
