using Application.Abstractions.Messaging;

namespace Application.Transactions;

public sealed record UpdateTransactionCommand(
    Guid Id,
    DateOnly Date,
    string Content,
    decimal CreditAmount,
    decimal DebitAmount,
    string? Note,
    string? Category,
    string? PaymentMethod = null,
    string? CardType = null,
    string? Bank = null,
    bool IsAdvance = false,
    Guid? AdvanceTransactionId = null) : ICommand;
