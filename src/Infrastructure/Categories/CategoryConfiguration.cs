using Domain.Categories;
using Domain.Users;
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

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.UserId);

        builder.Ignore(c => c.DomainEvents);
    }
}
