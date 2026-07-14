namespace Application.SubCategories.Data;

public sealed record SubCategoryResponse(
    Guid Id,
    string Category,
    string Name);
