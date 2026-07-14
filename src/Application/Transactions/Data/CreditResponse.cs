namespace Application.Transactions.Data;

public sealed record CreditResponse(
    Guid Id,
    DateOnly Date,
    string Content,
    MoneyResponse Credit);
