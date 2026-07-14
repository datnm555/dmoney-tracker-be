namespace Application.SubCategories.Data;

public sealed record SubCategoryResponse(
    Guid Id,
    Guid CategoryId,
    string Name,
    bool IsDefault,
    string? Icon = null);
