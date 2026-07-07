namespace Application.Transactions.Data;

public sealed record MonthlySummaryResponse(
    IReadOnlyList<TransactionResponse> Items,
    MoneyResponse TotalCredit,
    MoneyResponse TotalDebit,
    MoneyResponse Balance);
