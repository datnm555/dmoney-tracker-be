using Application.Abstractions.Messaging;

namespace Application.SubCategories;

public sealed record CreateSubCategoryCommand(string Category, string Name, bool IsDefault = false) : ICommand<Guid>;

public sealed record DeleteSubCategoryCommand(Guid Id) : ICommand;
