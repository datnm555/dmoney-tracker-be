using Application.Abstractions.Messaging;

namespace Application.Transactions;

public sealed record DeleteTransactionCommand(Guid Id) : ICommand;
