using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Lamina.Sut.Deployed.Tests;

// A real loopback service whose readiness path fails a set number of times before it answers 200.
internal sealed class FakeService : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly ConcurrentQueue<string> _requests = new();
    private int _failuresLeft;

    private FakeService(WebApplication app, int failures)
    {
        _app = app;
        _failuresLeft = failures;
    }

    public Uri BaseAddress => new(_app.Urls.Single());

    public IReadOnlyCollection<string> Requests => _requests;

    public static Task<FakeService> StartAsync() => StartAsync(failuresBeforeReady: 0);

    public static async Task<FakeService> StartAsync(int failuresBeforeReady)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        var service = new FakeService(app, failuresBeforeReady);
        app.Run(service.RespondAsync);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return service;
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    private Task RespondAsync(HttpContext context)
    {
        _requests.Enqueue(context.Request.Path);
        context.Response.StatusCode = Interlocked.Decrement(ref _failuresLeft) >= 0
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status200OK;
        return Task.CompletedTask;
    }
}
