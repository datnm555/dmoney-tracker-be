using SharedKernel;

namespace Domain.Categories;

/// <summary>
/// User-defined parent category. Built-in categories stay in
/// <see cref="Transactions.TransactionCategories"/>; transactions reference a custom
/// category by its Id (as a string) in their Category field.
/// </summary>
public sealed class Category : AuditedEntity
{
    private Category() { }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Icon key from the frontend's built-in icon set (later a CDN url).</summary>
    public string Icon { get; private set; } = string.Empty;

    /// <summary>
    /// Built-in code for seeded system categories (keeps i18n label and icon
    /// linkage across languages); null for user-created categories.
    /// </summary>
    public string? Code { get; private set; }

    public static Result<Category> Create(Guid userId, string name, string icon, string? code = null)
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
        if (trimmedIcon.Length == 0)
        {
            return Result.Failure<Category>(CategoryErrors.IconRequired);
        }

        if (trimmedIcon.Length > CategoryConstants.IconMaxLength)
        {
            return Result.Failure<Category>(CategoryErrors.IconRequired);
        }

        return new Category
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = trimmedName,
            Icon = trimmedIcon,
            Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim()
        };
    }
}
