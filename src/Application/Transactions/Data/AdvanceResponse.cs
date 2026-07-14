namespace Application.Transactions.Data;

public sealed record AdvanceResponse(
    Guid Id,
    DateOnly Date,
    string Content,
    MoneyResponse Debit);
