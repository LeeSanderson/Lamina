using Microsoft.EntityFrameworkCore;

namespace Folio.Ordering;

public sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : DbContext(options)
{
    private const int AmountPrecision = 12;
    private const int AmountScale = 2;
    private const int EnumMaxLength = 30;

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var order = modelBuilder.Entity<Order>();
        order.ToTable("orders");
        order.HasKey(o => o.Id);
        order.Property(o => o.Id).HasColumnName("id").ValueGeneratedNever();
        order.Property(o => o.CustomerId).HasColumnName("customer_id");
        order.HasIndex(o => o.CustomerId);
        order.Property(o => o.BookId).HasColumnName("book_id");
        order.Property(o => o.Quantity).HasColumnName("quantity");
        order.Property(o => o.Amount).HasColumnName("amount").HasPrecision(AmountPrecision, AmountScale);
        order.Property(o => o.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(EnumMaxLength);
        order.Property(o => o.RejectionReason).HasColumnName("rejection_reason").HasConversion<string>().HasMaxLength(EnumMaxLength);
        order.Property(o => o.PlacedAt).HasColumnName("placed_at");
    }
}
