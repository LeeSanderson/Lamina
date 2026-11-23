using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Lamina.Drivers.Http.Tests;

// A real loopback server, so requests go through the same HttpClient pipeline, propagator included, as against a real SUT.
// Each endpoint name is served under its own path prefix, so a request shows which endpoint's URI it was sent to.
internal sealed class FakeSut : ISystemUnderTest, ISutProvider
{
    private WebApplication? _app;
    private Uri? _root;

    public IReadOnlyDictionary<string, string> Metadata { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyCollection<string> EndpointNames { get; init; } = [];

    public Uri Endpoint(string name) => new(_root ?? throw new InvalidOperationException("The fake SUT isn't started."), $"{name}/");

    public async Task<ISystemUnderTest> StartAsync(CancellationToken ct)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        _app = builder.Build();
        _app.MapGet("/{**path}", (HttpRequest request) => Results.Ok(new Echo(request.Path, request.Headers["baggage"].ToArray())));
        _app.MapGet("/{endpoint}/problem", () => Results.Problem(
            title: "Out of stock",
            detail: "Dune has no stock left.",
            statusCode: StatusCodes.Status409Conflict,
            type: "https://folio.test/problems/out-of-stock",
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["bookId"] = "dune-k3f9" }));
        await _app.StartAsync(ct);
        _root = new Uri(_app.Urls.Single());
        return this;
    }

    public Task WaitUntilReadyAsync(CancellationToken ct) => Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}

public sealed record Echo(string Path, string?[] Baggage);
