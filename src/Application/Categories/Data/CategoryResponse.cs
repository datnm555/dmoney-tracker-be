namespace Application.Categories.Data;

public sealed record CategoryResponse(
    Guid Id,
    string Name,
    string Icon,
    string? Code = null);
