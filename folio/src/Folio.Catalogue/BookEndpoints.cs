using Microsoft.EntityFrameworkCore;

namespace Folio.Catalogue;

internal static class BookEndpoints
{
    internal static void MapBookEndpoints(this IEndpointRouteBuilder app)
    {
        var books = app.MapGroup("/books");
        books.MapPost("/", AddBookAsync);
        books.MapGet("/{id:guid}", GetBookAsync);
    }

    private static async Task<IResult> AddBookAsync(AddBookRequest request, CatalogueDbContext db, CancellationToken cancellationToken)
    {
        if (request.IsInvalid)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal) { ["book"] = ["A book needs a title, a price of zero or more, and stock of zero or more."] });
        }

        var book = new Book { Id = Guid.CreateVersion7(), Title = request.Title, Price = request.Price, OnHand = request.Stock };
        db.Books.Add(book);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created(new Uri($"/books/{book.Id}", UriKind.Relative), new BookAdded(book.Id));
    }

    private static async Task<IResult> GetBookAsync(Guid id, CatalogueDbContext db, CancellationToken cancellationToken)
    {
        var book = await db.Books.AsNoTracking().SingleOrDefaultAsync(b => b.Id == id, cancellationToken);
        return book is null
            ? Results.NotFound()
            : Results.Ok(new BookView(book.Id, book.Title, book.Price, book.Available));
    }

    internal sealed record AddBookRequest(string Title, decimal Price, int Stock)
    {
        internal bool IsInvalid => string.IsNullOrWhiteSpace(Title) || Price < 0 || Stock < 0;
    }

    internal sealed record BookAdded(Guid Id);

    internal sealed record BookView(Guid Id, string Title, decimal Price, int AvailableQuantity);
}
