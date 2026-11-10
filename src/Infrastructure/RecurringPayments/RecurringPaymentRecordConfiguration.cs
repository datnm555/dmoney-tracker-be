using Domain.RecurringPayments;
using Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.RecurringPayments;

internal sealed class RecurringPaymentRecordConfiguration : IEntityTypeConfiguration<RecurringPaymentRecord>
{
    public void Configure(EntityTypeBuilder<RecurringPaymentRecord> builder)
    {
        builder.ToTable("recurring_payment_records");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Period)
            .HasMaxLength(RecurringPaymentConstants.PeriodLength)
            .IsRequired();

        builder.Property(r => r.Amount).HasColumnType("numeric(18,2)");

        builder.HasOne<RecurringPayment>()
            .WithMany()
            .HasForeignKey(r => r.RecurringPaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Transaction>()
            .WithMany()
            .HasForeignKey(r => r.TransactionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(r => new { r.RecurringPaymentId, r.Period }).IsUnique();

        builder.Ignore(r => r.DomainEvents);
    }
}
