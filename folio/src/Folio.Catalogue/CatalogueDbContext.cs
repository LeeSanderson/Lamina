using Microsoft.EntityFrameworkCore;

namespace Folio.Catalogue;

public sealed class CatalogueDbContext(DbContextOptions<CatalogueDbContext> options) : DbContext(options)
{
    private const int TitleMaxLength = 500;
    private const int PricePrecision = 10;
    private const int PriceScale = 2;
    private const int StatusMaxLength = 20;

    public DbSet<Book> Books => Set<Book>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var book = modelBuilder.Entity<Book>();
        book.ToTable("books");
        book.HasKey(b => b.Id);
        book.Property(b => b.Id).HasColumnName("id").ValueGeneratedNever();
        book.Property(b => b.Title).HasColumnName("title").HasMaxLength(TitleMaxLength);
        book.Property(b => b.Price).HasColumnName("price").HasPrecision(PricePrecision, PriceScale);
        book.Property(b => b.OnHand).HasColumnName("on_hand");
        book.Property(b => b.Reserved).HasColumnName("reserved");
        book.Ignore(b => b.Available);
        book.ToTable(t => t.HasCheckConstraint("ck_books_stock", "reserved >= 0 AND reserved <= on_hand"));

        var reservation = modelBuilder.Entity<Reservation>();
        reservation.ToTable("reservations");
        reservation.HasKey(r => r.OrderId);
        reservation.Property(r => r.OrderId).HasColumnName("order_id").ValueGeneratedNever();
        reservation.Property(r => r.BookId).HasColumnName("book_id");
        reservation.HasOne<Book>().WithMany().HasForeignKey(r => r.BookId);
        reservation.Property(r => r.Quantity).HasColumnName("quantity");
        reservation.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(StatusMaxLength);
        reservation.ToTable(t => t.HasCheckConstraint("ck_reservations_quantity", "quantity > 0"));
    }
}
