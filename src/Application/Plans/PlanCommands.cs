using Application.Abstractions.Messaging;

namespace Application.Plans;

public sealed record CreatePlanCommand(string Name) : ICommand<Guid>;

public sealed record UpdatePlanCommand(Guid Id, string Name) : ICommand;

public sealed record DeletePlanCommand(Guid Id) : ICommand;
