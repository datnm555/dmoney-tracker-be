using Domain.RecurringPayments;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.RecurringPayments;

internal sealed class RecurringPaymentConfiguration : IEntityTypeConfiguration<RecurringPayment>
{
    public void Configure(EntityTypeBuilder<RecurringPayment> builder)
    {
        builder.ToTable("recurring_payments");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .HasMaxLength(RecurringPaymentConstants.NameMaxLength)
            .IsRequired();

        builder.Property(r => r.Amount).HasColumnType("numeric(18,2)");

        builder.Property(r => r.PaymentMethod)
            .HasMaxLength(RecurringPaymentConstants.PaymentMethodMaxLength);

        builder.Property(r => r.Bank).HasMaxLength(RecurringPaymentConstants.BankMaxLength);
        builder.Property(r => r.Note).HasMaxLength(RecurringPaymentConstants.NoteMaxLength);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.UserId);

        builder.Ignore(r => r.DomainEvents);
    }
}
