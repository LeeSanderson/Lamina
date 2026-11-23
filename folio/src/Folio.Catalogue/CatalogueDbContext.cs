using Microsoft.EntityFrameworkCore;

namespace Folio.Catalogue;

public sealed class CatalogueDbContext(DbContextOptions<CatalogueDbContext> options) : DbContext(options)
{
    private const int TitleMaxLength = 500;
    private const int PricePrecision = 10;
    private const int PriceScale = 2;

    public DbSet<Book> Books => Set<Book>();

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
    }
}
