namespace Folio.Customers;

public sealed class Customer
{
    public Guid Id { get; init; }

    public required string Subject { get; init; }
}
