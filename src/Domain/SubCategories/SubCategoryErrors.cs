using SharedKernel;

namespace Domain.SubCategories;

public static class SubCategoryErrors
{
    public static readonly Error NameRequired = Error.Validation(
        "SubCategories.NameRequired",
        "Please enter a sub-category name.");

    public static readonly Error NameTooLong = Error.Validation(
        "SubCategories.NameTooLong",
        $"Sub-category name must be at most {SubCategoryConstants.NameMaxLength} characters.");

    public static readonly Error InvalidCategory = Error.Validation(
        "SubCategories.InvalidCategory",
        "Invalid parent category.");

    public static readonly Error Duplicate = Error.Conflict(
        "SubCategories.Duplicate",
        "This sub-category already exists.");

    public static readonly Error NotFound = Error.NotFound(
        "SubCategories.NotFound",
        "Sub-category not found.");

    public static readonly Error CategoryMismatch = Error.Validation(
        "SubCategories.CategoryMismatch",
        "The sub-category does not belong to the selected category.");

    public static readonly Error InvalidIcon = Error.Validation(
        "SubCategories.InvalidIcon",
        "Invalid sub-category icon.");
}
