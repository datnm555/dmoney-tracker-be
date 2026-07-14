using Domain.SubCategories;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.SubCategories;

internal sealed class SubCategoryConfiguration : IEntityTypeConfiguration<SubCategory>
{
    public void Configure(EntityTypeBuilder<SubCategory> builder)
    {
        builder.ToTable("sub_categories");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Category)
            .HasMaxLength(Domain.Transactions.TransactionCategories.MaxLength)
            .IsRequired();

        builder.Property(s => s.Name)
            .HasMaxLength(SubCategoryConstants.NameMaxLength)
            .IsRequired();

        builder.Property(s => s.IsDefault)
            .HasDefaultValue(false);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.UserId, s.Category });

        builder.Ignore(s => s.DomainEvents);
    }
}
