using Domain.Plans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Plans;

internal sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("plans");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .HasMaxLength(PlanConstants.NameMaxLength)
            .IsRequired();

        builder.HasIndex(p => p.UserId);

        builder.Ignore(p => p.DomainEvents);
    }
}
