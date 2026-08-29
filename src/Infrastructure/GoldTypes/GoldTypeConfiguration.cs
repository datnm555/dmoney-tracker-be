using Domain.GoldTypes;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.GoldTypes;

internal sealed class GoldTypeConfiguration : IEntityTypeConfiguration<GoldType>
{
    public void Configure(EntityTypeBuilder<GoldType> builder)
    {
        builder.ToTable("gold_types");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name)
            .HasMaxLength(GoldTypeConstants.NameMaxLength)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(g => g.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(g => g.UserId);

        builder.Ignore(g => g.DomainEvents);
    }
}
