namespace Application.Transactions.Data;

public sealed record TransactionResponse(
    Guid Id,
    DateOnly Date,
    string Content,
    MoneyResponse Credit,
    MoneyResponse Debit,
    string? Note,
    Guid? CategoryId,
    string PaymentMethod,
    string? CardType,
    string? Bank,
    bool IsAdvance,
    IReadOnlyList<Guid> AdvanceTransactionIds,
    bool IsPrepaid,
    DateOnly? PrepaidFrom,
    DateOnly? PrepaidTo,
    Guid? PrepaidTransactionId,
    Guid? SubCategoryId,
    string? SubCategoryName,
    Guid? ReimbursedByTransactionId = null,
    IReadOnlyList<LinkedTransactionResponse>? Links = null);

public sealed record LinkedTransactionResponse(
    Guid Id,
    DateOnly Date,
    string Content,
    MoneyResponse Credit,
    MoneyResponse Debit,
    string Relation);
