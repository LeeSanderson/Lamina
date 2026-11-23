using System.Net;

namespace Folio.WebBff;

internal static class BookEndpoints
{
    internal static void MapBookEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapPost("/staff/books", AddBookAsync).RequireAuthorization(TestTokenAuthentication.StaffPolicy);
        api.MapGet("/books/{id:guid}", GetBookAsync);
    }

    private static async Task<IResult> AddBookAsync(AddBookRequest request, CatalogueClient catalogue, CancellationToken cancellationToken)
    {
        using var response = await catalogue.AddBookAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(cancellationToken);
            return Results.ValidationProblem(problem?.Errors ?? new Dictionary<string, string[]>(StringComparer.Ordinal));
        }

        response.EnsureSuccessStatusCode();
        var added = await response.Content.ReadFromJsonAsync<BookAdded>(cancellationToken);
        return Results.Created(new Uri($"/api/books/{added!.Id}", UriKind.Relative), added);
    }

    private static async Task<IResult> GetBookAsync(Guid id, CatalogueClient catalogue, CancellationToken cancellationToken)
    {
        var book = await catalogue.GetBookAsync(id, cancellationToken);
        return book is null
            ? Results.NotFound()
            : Results.Ok(new BookView(book.Id, book.Title, book.Price, book.AvailableQuantity));
    }

    internal sealed record BookAdded(Guid Id);

    internal sealed record BookView(Guid Id, string Title, decimal Price, int AvailableQuantity);
}

internal sealed record AddBookRequest(string Title, decimal Price, int Stock);
