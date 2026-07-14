using Application.Abstractions.Messaging;

namespace Application.SubCategories;

public sealed record CreateSubCategoryCommand(string Category, string Name) : ICommand<Guid>;

public sealed record DeleteSubCategoryCommand(Guid Id) : ICommand;
