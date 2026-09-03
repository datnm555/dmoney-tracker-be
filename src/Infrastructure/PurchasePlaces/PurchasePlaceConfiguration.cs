using Domain.PurchasePlaces;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.PurchasePlaces;

internal sealed class PurchasePlaceConfiguration : IEntityTypeConfiguration<PurchasePlace>
{
    public void Configure(EntityTypeBuilder<PurchasePlace> builder)
    {
        builder.ToTable("purchase_places");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .HasMaxLength(PurchasePlaceConstants.NameMaxLength)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.UserId);

        builder.Ignore(p => p.DomainEvents);
    }
}
