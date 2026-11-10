using Application.Accounts.Data;
using Application.Transactions.Data;
using Domain.Accounts;
using Domain.Transactions;

namespace Application.Accounts;

internal static class AccountSnapshotMapping
{
    public static AccountSnapshotResponse ToResponse(this AccountSnapshot s) => new(
        s.Id,
        s.AccountId,
        s.Date,
        Vnd(s.Balance),
        s.StatementAmount is { } statement ? Vnd(statement) : null,
        s.DueDate,
        s.InstallmentRemaining is { } installment ? Vnd(installment) : null,
        s.StatementPaid,
        s.Note);

    private static MoneyResponse Vnd(decimal amount) => new(amount, Money.DefaultCurrency);
}
