using Microsoft.EntityFrameworkCore;

namespace Folio.Customers;

internal static class CustomerEndpoints
{
    internal static void MapCustomerEndpoints(this IEndpointRouteBuilder app) =>
        app.MapPut("/customers/{subject}", EnsureCustomerAsync);

    private static async Task<IResult> EnsureCustomerAsync(string subject, CustomersDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO customers (id, subject) VALUES ({Guid.CreateVersion7()}, {subject}) ON CONFLICT (subject) DO NOTHING",
            cancellationToken);
        var id = await db.Customers.Where(c => c.Subject == subject).Select(c => c.Id).SingleAsync(cancellationToken);
        return Results.Ok(new CustomerView(id, subject));
    }

    internal sealed record CustomerView(Guid Id, string Subject);
}
