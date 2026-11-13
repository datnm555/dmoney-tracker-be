using Domain.Accounts;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Accounts;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .HasMaxLength(AccountConstants.NameMaxLength)
            .IsRequired();

        builder.Property(a => a.Type)
            .HasMaxLength(AccountConstants.TypeMaxLength)
            .IsRequired();

        builder.Property(a => a.LastDigits)
            .HasMaxLength(AccountConstants.LastDigitsLength);

        builder.Property(a => a.BankName)
            .HasMaxLength(AccountConstants.BankNameMaxLength);

        builder.Property(a => a.IsDefault)
            .HasDefaultValue(false);

        builder.Property(a => a.CreditLimit).HasColumnType("numeric(18,2)");
        builder.Property(a => a.InterestRatePercent).HasColumnType("numeric(5,2)");
        builder.Property(a => a.UntrackedDebt).HasColumnType("numeric(18,2)");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.UserId);

        builder.Ignore(a => a.DomainEvents);
    }
}

internal sealed class AccountSnapshotConfiguration : IEntityTypeConfiguration<AccountSnapshot>
{
    public void Configure(EntityTypeBuilder<AccountSnapshot> builder)
    {
        builder.ToTable("account_snapshots");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Balance).HasColumnType("numeric(18,2)");
        builder.Property(s => s.StatementAmount).HasColumnType("numeric(18,2)");
        builder.Property(s => s.InstallmentRemaining).HasColumnType("numeric(18,2)");

        builder.Property(s => s.Note).HasMaxLength(AccountConstants.NoteMaxLength);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(s => s.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.AccountId, s.Date });

        builder.Ignore(s => s.DomainEvents);
    }
}
