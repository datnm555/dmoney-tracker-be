using Application.Abstractions.Messaging;

namespace Application.PurchasePlaces;

public sealed record CreatePurchasePlaceCommand(string Name) : ICommand<Guid>;

public sealed record UpdatePurchasePlaceCommand(Guid Id, string Name) : ICommand;

public sealed record DeletePurchasePlaceCommand(Guid Id) : ICommand;
