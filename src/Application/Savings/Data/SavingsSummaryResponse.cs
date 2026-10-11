using Application.Transactions.Data;

namespace Application.Savings.Data;

public sealed record SavingsSummaryResponse(
    Guid PlanId,
    string PlanName,
    MoneyResponse TotalBalance,
    IReadOnlyList<SavingsAccountResponse> Accounts,
    IReadOnlyList<SavingsTransactionResponse> Transactions);

public sealed record SavingsAccountResponse(
    string? SavingsBank,
    MoneyResponse Balance,
    MoneyResponse TotalDeposited,
    MoneyResponse TotalWithdrawn,
    int TransactionCount);

public sealed record SavingsTransactionResponse(
    Guid TransactionId,
    DateOnly Date,
    string Content,
    string? SavingsBank,
    string? Bank,
    MoneyResponse Credit,
    MoneyResponse Debit,
    Guid PlanId,
    string? SubCategoryName,
    string? Note);
