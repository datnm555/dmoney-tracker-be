using Application.Abstractions.Messaging;

namespace Application.Categories;

public sealed record CreateCategoryCommand(string Name, string Icon) : ICommand<Guid>;

public sealed record DeleteCategoryCommand(Guid Id) : ICommand;
