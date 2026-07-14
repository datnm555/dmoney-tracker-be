using Application.Abstractions.Messaging;

namespace Application.SubCategories;

public sealed record CreateSubCategoryCommand(
    Guid CategoryId,
    string Name,
    bool IsDefault = false,
    string? Icon = null) : ICommand<Guid>;

public sealed record UpdateSubCategoryCommand(
    Guid Id,
    string Name,
    bool IsDefault = false,
    string? Icon = null) : ICommand;

public sealed record DeleteSubCategoryCommand(Guid Id) : ICommand;
