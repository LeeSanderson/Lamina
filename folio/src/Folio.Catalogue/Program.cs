using Folio.Catalogue;
using Folio.ServiceDefaults;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<CatalogueDbContext>("catalogue-db");

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapBookEndpoints();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<CatalogueDbContext>().Database.MigrateAsync();
}

await app.RunAsync();
