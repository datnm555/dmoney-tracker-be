using Application.Transactions.Data;

namespace Application.Accounts.Data;

public sealed record AccountSnapshotResponse(
    Guid Id, Guid AccountId, DateOnly Date, MoneyResponse Balance,
    MoneyResponse? StatementAmount, DateOnly? DueDate,
    MoneyResponse? InstallmentRemaining, bool StatementPaid, string? Note,
    bool StatementUntracked);
