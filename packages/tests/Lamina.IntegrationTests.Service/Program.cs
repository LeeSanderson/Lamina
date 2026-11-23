using System.Globalization;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);
var healthyAfter = TimeSpan.Parse(builder.Configuration["HealthyAfter"] ?? "00:00:00", CultureInfo.InvariantCulture);
var started = TimeProvider.System.GetTimestamp();
builder.Services.AddHealthChecks().AddCheck(
    "warm-up",
    () => TimeProvider.System.GetElapsedTime(started) >= healthyAfter ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Still warming up."));

var app = builder.Build();
app.MapGet("/", () => app.Configuration["Greeting"] ?? string.Empty);
app.MapHealthChecks("/health");
await app.RunAsync().ConfigureAwait(false);
