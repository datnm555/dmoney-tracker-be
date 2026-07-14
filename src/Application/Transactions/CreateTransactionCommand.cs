using Application.Abstractions.Messaging;

namespace Application.Transactions;

public sealed record CreateTransactionCommand(
    DateOnly Date,
    string Content,
    decimal CreditAmount,
    decimal DebitAmount,
    string? Note,
    Guid? CategoryId,
    string? PaymentMethod = null,
    string? CardType = null,
    string? Bank = null,
    bool IsAdvance = false,
    IReadOnlyList<Guid>? AdvanceTransactionIds = null,
    bool IsPrepaid = false,
    DateOnly? PrepaidFrom = null,
    DateOnly? PrepaidTo = null,
    Guid? PrepaidTransactionId = null,
    Guid? SubCategoryId = null) : ICommand<Guid>;
