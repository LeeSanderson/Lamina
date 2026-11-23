using Microsoft.EntityFrameworkCore;

namespace Folio.Customers;

public sealed class CustomersDbContext(DbContextOptions<CustomersDbContext> options) : DbContext(options)
{
    private const int SubjectMaxLength = 200;

    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var customer = modelBuilder.Entity<Customer>();
        customer.ToTable("customers");
        customer.HasKey(c => c.Id);
        customer.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        customer.Property(c => c.Subject).HasColumnName("subject").HasMaxLength(SubjectMaxLength);
        customer.HasIndex(c => c.Subject).IsUnique();
    }
}
