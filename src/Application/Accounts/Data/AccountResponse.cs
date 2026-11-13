using Application.Transactions.Data;

namespace Application.Accounts.Data;

public sealed record AccountResponse(
    Guid Id, string Name, string Type, string? LastDigits,
    MoneyResponse? CreditLimit, int? StatementDay, int? DueDay,
    decimal? InterestRatePercent, AccountSnapshotResponse? LatestSnapshot,
    string? BankName = null, bool IsDefault = false,
    MoneyResponse? CurrentBalance = null, int TransactionsSinceSnapshot = 0,
    MoneyResponse? StatementPaidAmount = null,
    MoneyResponse? UntrackedDebt = null, MoneyResponse? UntrackedDebtRemaining = null);
