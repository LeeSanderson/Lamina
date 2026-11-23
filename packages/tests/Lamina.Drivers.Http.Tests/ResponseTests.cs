using System.Net;

namespace Lamina.Drivers.Http.Tests;

public sealed class ResponseTests : IAsyncLifetime
{
    private LaminaRun? _run;
    private SpecScope? _scope;

    private HttpClient Client => _scope.ShouldNotBeNull().Services.GetRequiredService<SutHttpClients>().For("web-bff");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AProblemDetailsResponseIsReadIntoAProblem()
    {
        using var response = await Client.GetAsync(new Uri("problem", UriKind.Relative), Cancellation);

        var problem = await response.ReadProblemAsync(Cancellation);

        problem.Status.ShouldBe(409);
        problem.Title.ShouldBe("Out of stock");
        problem.Detail.ShouldBe("Dune has no stock left.");
        problem.Type.ShouldBe("https://folio.test/problems/out-of-stock");
        problem.Extensions["bookId"].GetString().ShouldBe("dune-k3f9");
    }

    [Fact]
    public async Task AProblemDetailsResponseIsReadIntoATeamsOwnProblemType()
    {
        using var response = await Client.GetAsync(new Uri("problem", UriKind.Relative), Cancellation);

        var problem = await response.ReadProblemAsync<OutOfStock>(Cancellation);

        problem.ShouldBe(new OutOfStock("Out of stock", "dune-k3f9"));
    }

    [Fact]
    public async Task ReadingAProblemFromASuccessfulResponseFailsWithItsStatus()
    {
        using var response = await Client.GetAsync(new Uri("api/books", UriKind.Relative), Cancellation);

        var failure = await Should.ThrowAsync<HttpRequestException>(() => response.ReadProblemAsync(Cancellation));

        failure.Message.ShouldContain("200");
    }

    [Fact]
    public async Task ASuccessfulResponseIsReadAsJson()
    {
        using var response = await Client.GetAsync(new Uri("api/books", UriKind.Relative), Cancellation);

        var echo = await response.ReadJsonAsync<Echo>(Cancellation);

        echo.Path.ShouldBe("/web-bff/api/books");
    }

    [Fact]
    public async Task ReadingJsonFromAFailedResponseShowsTheStatusAndTheProblem()
    {
        using var response = await Client.GetAsync(new Uri("problem", UriKind.Relative), Cancellation);

        var failure = await Should.ThrowAsync<HttpProblemException>(() => response.ReadJsonAsync<Echo>(Cancellation));

        failure.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        failure.Problem.ShouldNotBeNull().Title.ShouldBe("Out of stock");
        failure.Message.ShouldContain("409");
        failure.Message.ShouldContain("Dune has no stock left.");
    }

    public async ValueTask InitializeAsync()
    {
        _run = await HttpSuite.StartAsync(lamina => lamina.AddHttpClient("web-bff"));
        _scope = _run.CreateScope(channel: null, specName: "spec");
    }

    public async ValueTask DisposeAsync()
    {
        if (_scope is not null)
        {
            await _scope.DisposeAsync();
        }

        if (_run is not null)
        {
            await _run.DisposeAsync();
        }
    }

    public sealed record OutOfStock(string Title, string BookId);
}
