using SharedKernel;

namespace Domain.Categories;

/// <summary>
/// Shared parent category (no per-user ownership; admin management comes
/// later). Transactions and sub-categories reference it by CategoryId.
/// </summary>
public sealed class Category : AuditedEntity
{
    private Category() { }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Icon key from the frontend's built-in icon set (later a CDN url).</summary>
    public string Icon { get; private set; } = string.Empty;

    /// <summary>
    /// Built-in code for seeded system categories (keeps i18n label and icon
    /// linkage across languages); null for user-created categories.
    /// </summary>
    public string? Code { get; private set; }

    /// <summary>Username of whoever created the category.</summary>
    public string CreatedBy { get; private set; } = string.Empty;

    /// <summary>Username of whoever last changed the category.</summary>
    public string? UpdatedBy { get; private set; }

    public static Result<Category> Create(string name, string icon, string createdBy, string? code = null)
    {
        string trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length == 0)
        {
            return Result.Failure<Category>(CategoryErrors.NameRequired);
        }

        if (trimmedName.Length > CategoryConstants.NameMaxLength)
        {
            return Result.Failure<Category>(CategoryErrors.NameTooLong);
        }

        string trimmedIcon = icon?.Trim() ?? string.Empty;
        if (trimmedIcon.Length is 0 or > CategoryConstants.IconMaxLength)
        {
            return Result.Failure<Category>(CategoryErrors.IconRequired);
        }

        string trimmedCreatedBy = createdBy?.Trim() ?? string.Empty;

        return new Category
        {
            Id = Guid.CreateVersion7(),
            Name = trimmedName,
            Icon = trimmedIcon,
            Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim(),
            CreatedBy = trimmedCreatedBy,
            UpdatedBy = trimmedCreatedBy
        };
    }
}
