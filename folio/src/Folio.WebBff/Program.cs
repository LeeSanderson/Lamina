using Folio.ServiceDefaults;
using Folio.WebBff;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddTestTokenAuthentication();
builder.AddFolioServiceClients();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();
app.MapBookEndpoints();
await app.RunAsync();
