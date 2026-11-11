using Domain.RecurringPayments;
using Domain.Savings;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Savings;

internal sealed class SavingsGoalConfiguration : IEntityTypeConfiguration<SavingsGoal>
{
    public void Configure(EntityTypeBuilder<SavingsGoal> builder)
    {
        builder.ToTable("savings_goals");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.SavingsBank).HasMaxLength(SavingsGoal.SavingsBankMaxLength).IsRequired();
        builder.Property(g => g.TargetAmount).HasColumnType("numeric(18,2)");
        builder.Property(g => g.InitialAmount).HasColumnType("numeric(18,2)");
        builder.Property(g => g.StartPeriod).HasMaxLength(RecurringPaymentConstants.PeriodLength).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(g => g.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // One goal per savings place.
        builder.HasIndex(g => new { g.UserId, g.SavingsBank }).IsUnique();

        builder.Ignore(g => g.DomainEvents);
    }
}
