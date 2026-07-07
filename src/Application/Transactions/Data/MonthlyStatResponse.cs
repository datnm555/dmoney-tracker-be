namespace Application.Transactions.Data;

public sealed record MonthlyStatResponse(
    string Month,
    MoneyResponse TotalCredit,
    MoneyResponse TotalDebit,
    MoneyResponse Balance);
