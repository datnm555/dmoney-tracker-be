using Application.Abstractions.Messaging;

namespace Application.Transactions;

public sealed record CreateTransactionCommand(
    DateOnly Date,
    string Content,
    decimal CreditAmount,
    decimal DebitAmount,
    string? Note,
    string? Category,
    string? PaymentMethod = null,
    string? CardType = null,
    string? Bank = null,
    bool IsAdvance = false) : ICommand<Guid>;
