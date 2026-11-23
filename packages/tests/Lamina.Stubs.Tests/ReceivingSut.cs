using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Lamina.Stubs.Tests;

// A real loopback server standing in for the system under test, so webhooks go through the same HttpClient pipeline,
// baggage propagator included, as against a real SUT. Each endpoint name is served under its own path prefix. It records
// every POST, and answers 422 to any path ending in /refuse.
internal sealed class ReceivingSut : ISystemUnderTest, ISutProvider
{
    private WebApplication? _app;
    private Uri? _root;

    public ConcurrentQueue<ReceivedWebhook> Received { get; } = new();

    public IReadOnlyDictionary<string, string> Metadata { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public Uri Endpoint(string name) => new(_root ?? throw new InvalidOperationException("The receiving SUT isn't started."), $"{name}/");

    public async Task<ISystemUnderTest> StartAsync(CancellationToken ct)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        _app = builder.Build();
        _app.MapPost("/{**path}", async (HttpRequest request, CancellationToken requestAborted) =>
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync(requestAborted);
            Received.Enqueue(new ReceivedWebhook(request.Path, request.ContentType, body, request.Headers["baggage"].ToString()));
            return request.Path.Value!.EndsWith("/refuse", StringComparison.Ordinal)
                ? Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Unknown payment reference")
                : Results.Ok();
        });
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

internal sealed record ReceivedWebhook(string Path, string? ContentType, string Body, string Baggage);
