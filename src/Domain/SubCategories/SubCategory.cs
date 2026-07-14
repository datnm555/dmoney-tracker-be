using Domain.Transactions;
using SharedKernel;

namespace Domain.SubCategories;

public sealed class SubCategory : AuditedEntity
{
    private SubCategory() { }

    public Guid UserId { get; private set; }

    /// <summary>Parent category code from <see cref="TransactionCategories"/>.</summary>
    public string Category { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public static Result<SubCategory> Create(Guid userId, string category, string name)
    {
        string trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length == 0)
        {
            return Result.Failure<SubCategory>(SubCategoryErrors.NameRequired);
        }

        if (trimmedName.Length > SubCategoryConstants.NameMaxLength)
        {
            return Result.Failure<SubCategory>(SubCategoryErrors.NameTooLong);
        }

        string trimmedCategory = category?.Trim() ?? string.Empty;
        if (!TransactionCategories.IsValid(trimmedCategory))
        {
            return Result.Failure<SubCategory>(SubCategoryErrors.InvalidCategory);
        }

        return new SubCategory
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Category = trimmedCategory,
            Name = trimmedName
        };
    }
}
