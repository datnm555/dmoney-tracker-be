using SharedKernel;

namespace Domain.Categories;

public static class CategoryErrors
{
    public static readonly Error NameRequired = Error.Validation(
        "Categories.NameRequired",
        "Please enter a category name.");

    public static readonly Error NameTooLong = Error.Validation(
        "Categories.NameTooLong",
        $"Category name must be at most {CategoryConstants.NameMaxLength} characters.");

    public static readonly Error IconRequired = Error.Validation(
        "Categories.IconRequired",
        "Please pick an icon for the category.");

    public static readonly Error Duplicate = Error.Conflict(
        "Categories.Duplicate",
        "This category already exists.");

    public static readonly Error NotFound = Error.NotFound(
        "Categories.NotFound",
        "Category not found.");

    public static readonly Error InUse = Error.Conflict(
        "Categories.InUse",
        "This category is used by transactions or sub-categories and cannot be deleted.");
}
