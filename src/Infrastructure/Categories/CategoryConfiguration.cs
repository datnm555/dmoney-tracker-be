using Domain.Categories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Categories;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .HasMaxLength(CategoryConstants.NameMaxLength)
            .IsRequired();

        builder.Property(c => c.Icon)
            .HasMaxLength(CategoryConstants.IconMaxLength)
            .IsRequired();

        builder.Property(c => c.Code)
            .HasMaxLength(CategoryConstants.CodeMaxLength);

        builder.Property(c => c.CreatedBy)
            .HasMaxLength(CategoryConstants.AuditNameMaxLength)
            .IsRequired();

        builder.Property(c => c.UpdatedBy)
            .HasMaxLength(CategoryConstants.AuditNameMaxLength);

        builder.Ignore(c => c.DomainEvents);
    }
}
