using Domain.Beneficiaries;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Beneficiaries;

internal sealed class BeneficiaryConfiguration : IEntityTypeConfiguration<Beneficiary>
{
    public void Configure(EntityTypeBuilder<Beneficiary> builder)
    {
        builder.ToTable("beneficiaries");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name)
            .HasMaxLength(BeneficiaryConstants.NameMaxLength)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(b => b.UserId);

        builder.Ignore(b => b.DomainEvents);
    }
}
