using SharedKernel;

namespace Domain.SubCategories;

/// <summary>
/// Shared sub-category: belongs to a parent category only (no per-user
/// ownership; admin management comes later).
/// </summary>
public sealed class SubCategory : AuditedEntity
{
    private SubCategory() { }

    /// <summary>Parent category (categories table).</summary>
    public Guid CategoryId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Auto-picked in the transaction dialog when its parent category is chosen.</summary>
    public bool IsDefault { get; private set; }

    /// <summary>Icon key from the frontend's built-in icon set (later a CDN url).</summary>
    public string? Icon { get; private set; }

    /// <summary>Username of whoever created the sub-category.</summary>
    public string CreatedBy { get; private set; } = string.Empty;

    /// <summary>Username of whoever last changed the sub-category.</summary>
    public string? UpdatedBy { get; private set; }

    public static Result<SubCategory> Create(
        Guid categoryId,
        string name,
        string createdBy,
        bool isDefault = false,
        string? icon = null)
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

        if (categoryId == Guid.Empty)
        {
            return Result.Failure<SubCategory>(SubCategoryErrors.InvalidCategory);
        }

        string? trimmedIcon = string.IsNullOrWhiteSpace(icon) ? null : icon.Trim();
        if (trimmedIcon is { Length: > SubCategoryConstants.IconMaxLength })
        {
            return Result.Failure<SubCategory>(SubCategoryErrors.InvalidIcon);
        }

        string trimmedCreatedBy = createdBy?.Trim() ?? string.Empty;

        return new SubCategory
        {
            Id = Guid.CreateVersion7(),
            CategoryId = categoryId,
            Name = trimmedName,
            IsDefault = isDefault,
            Icon = trimmedIcon,
            CreatedBy = trimmedCreatedBy,
            UpdatedBy = trimmedCreatedBy
        };
    }

    public Result Update(string name, bool isDefault, string? icon, string updatedBy)
    {
        string trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length == 0)
        {
            return Result.Failure(SubCategoryErrors.NameRequired);
        }

        if (trimmedName.Length > SubCategoryConstants.NameMaxLength)
        {
            return Result.Failure(SubCategoryErrors.NameTooLong);
        }

        string? trimmedIcon = string.IsNullOrWhiteSpace(icon) ? null : icon.Trim();
        if (trimmedIcon is { Length: > SubCategoryConstants.IconMaxLength })
        {
            return Result.Failure(SubCategoryErrors.InvalidIcon);
        }

        Name = trimmedName;
        IsDefault = isDefault;
        Icon = trimmedIcon;
        UpdatedBy = updatedBy?.Trim() ?? string.Empty;

        return Result.Success();
    }

    public void SetDefault(bool isDefault) => IsDefault = isDefault;
}
