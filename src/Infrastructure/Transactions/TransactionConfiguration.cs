using Domain.Transactions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Transactions;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Content)
            .HasMaxLength(TransactionConstants.ContentMaxLength)
            .IsRequired();

        builder.Property(t => t.Note)
            .HasMaxLength(TransactionConstants.NoteMaxLength);

        builder.OwnsOne(t => t.Credit, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("credit_amount")
                .HasColumnType("numeric(18,2)");
            money.Property(m => m.Currency)
                .HasColumnName("credit_currency")
                .HasMaxLength(3)
                .IsFixedLength();
        });

        builder.OwnsOne(t => t.Debit, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("debit_amount")
                .HasColumnType("numeric(18,2)");
            money.Property(m => m.Currency)
                .HasColumnName("debit_currency")
                .HasMaxLength(3)
                .IsFixedLength();
        });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => new { t.UserId, t.Date });

        builder.Ignore(t => t.DomainEvents);

        builder.Property(t => t.Category)
            .HasMaxLength(TransactionCategories.MaxLength);

        builder.Property(t => t.PaymentMethod)
            .HasMaxLength(PaymentMethods.MaxLength)
            .IsRequired()
            .HasDefaultValue(PaymentMethods.Transfer);

        builder.Property(t => t.CardType)
            .HasMaxLength(CardTypes.MaxLength);

        builder.Property(t => t.Bank)
            .HasMaxLength(TransactionConstants.BankMaxLength);

        builder.Property(t => t.IsAdvance)
            .HasDefaultValue(false);
    }
}
