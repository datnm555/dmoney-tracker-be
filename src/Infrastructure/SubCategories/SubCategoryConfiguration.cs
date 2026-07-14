using Domain.SubCategories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.SubCategories;

internal sealed class SubCategoryConfiguration : IEntityTypeConfiguration<SubCategory>
{
    public void Configure(EntityTypeBuilder<SubCategory> builder)
    {
        builder.ToTable("sub_categories");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .HasMaxLength(SubCategoryConstants.NameMaxLength)
            .IsRequired();

        builder.Property(s => s.IsDefault)
            .HasDefaultValue(false);

        builder.Property(s => s.Icon)
            .HasMaxLength(SubCategoryConstants.IconMaxLength);

        builder.Property(s => s.CreatedBy)
            .HasMaxLength(Domain.Categories.CategoryConstants.AuditNameMaxLength)
            .IsRequired();

        builder.Property(s => s.UpdatedBy)
            .HasMaxLength(Domain.Categories.CategoryConstants.AuditNameMaxLength);

        builder.HasOne<Domain.Categories.Category>()
            .WithMany()
            .HasForeignKey(s => s.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.CategoryId);

        builder.Ignore(s => s.DomainEvents);
    }
}
