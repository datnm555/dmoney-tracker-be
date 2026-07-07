using Application.Abstractions.Messaging;

namespace Application.Transactions;

public sealed record UpdateTransactionCommand(
    Guid Id,
    DateOnly Date,
    string Content,
    decimal CreditAmount,
    decimal DebitAmount,
    string? Note,
    string? Category) : ICommand;
