using Application.Abstractions.Messaging;

namespace Application.GoldTypes;

public sealed record CreateGoldTypeCommand(string Name) : ICommand<Guid>;

public sealed record UpdateGoldTypeCommand(Guid Id, string Name) : ICommand;

public sealed record DeleteGoldTypeCommand(Guid Id) : ICommand;
