using Domain.GoldAcquisitions;
using Domain.GoldTypes;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.GoldAcquisitions;

internal sealed class GoldAcquisitionConfiguration : IEntityTypeConfiguration<GoldAcquisition>
{
    public void Configure(EntityTypeBuilder<GoldAcquisition> builder)
    {
        builder.ToTable("gold_acquisitions");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Quantity)
            .HasColumnType("numeric(18,4)");

        builder.Property(a => a.UnitPrice)
            .HasColumnType("numeric(18,2)");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<GoldType>()
            .WithMany()
            .HasForeignKey(a => a.GoldTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.PurchasePlaces.PurchasePlace>()
            .WithMany()
            .HasForeignKey(a => a.PurchasePlaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.UserId);

        builder.Ignore(a => a.DomainEvents);
    }
}
