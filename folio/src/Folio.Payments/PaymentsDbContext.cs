using Microsoft.EntityFrameworkCore;

namespace Folio.Payments;

public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    private const int AmountPrecision = 12;
    private const int AmountScale = 2;
    private const int TextMaxLength = 200;

    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var payment = modelBuilder.Entity<Payment>();
        payment.ToTable("payments");
        payment.HasKey(p => p.Id);
        payment.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        payment.Property(p => p.OrderId).HasColumnName("order_id");
        payment.HasIndex(p => p.OrderId).IsUnique();
        payment.Property(p => p.Reference).HasColumnName("reference").HasMaxLength(TextMaxLength);
        payment.HasIndex(p => p.Reference).IsUnique();
        payment.Property(p => p.IdempotencyKey).HasColumnName("idempotency_key");
        payment.Property(p => p.Amount).HasColumnName("amount").HasPrecision(AmountPrecision, AmountScale);
        payment.Property(p => p.CardToken).HasColumnName("card_token").HasMaxLength(TextMaxLength);
        payment.Property(p => p.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(TextMaxLength);
        payment.Property(p => p.ProviderPaymentId).HasColumnName("provider_payment_id").HasMaxLength(TextMaxLength);
        payment.Property(p => p.CreatedAt).HasColumnName("created_at");
        payment.Property(p => p.NextAttemptAt).HasColumnName("next_attempt_at");
        payment.Property(p => p.Attempts).HasColumnName("attempts");
        payment.Ignore(p => p.AmountInPence);
        payment.Ignore(p => p.Outcome);
    }
}
