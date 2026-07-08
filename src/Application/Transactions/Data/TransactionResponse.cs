namespace Application.Transactions.Data;

public sealed record TransactionResponse(
    Guid Id,
    DateOnly Date,
    string Content,
    MoneyResponse Credit,
    MoneyResponse Debit,
    string? Note,
    string? Category,
    string PaymentMethod,
    string? CardType,
    string? Bank,
    bool IsAdvance,
    Guid? AdvanceTransactionId);
