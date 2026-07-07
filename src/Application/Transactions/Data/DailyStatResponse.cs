namespace Application.Transactions.Data;

public sealed record DailyStatResponse(int Day, MoneyResponse Debit);
