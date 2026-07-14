namespace Application.Transactions.Data;

public sealed record CategoryStatResponse(Guid? CategoryId, MoneyResponse Debit);
