namespace Application.Transactions.Data;

public sealed record PrepaidCreditResponse(
    Guid Id,
    DateOnly Date,
    string Content,
    MoneyResponse Credit,
    DateOnly? PrepaidFrom,
    DateOnly? PrepaidTo);
