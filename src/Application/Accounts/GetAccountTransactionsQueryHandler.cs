using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Accounts.Data;
using Application.Transactions.Data;
using Domain.Accounts;
using Domain.Transactions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Accounts;

internal sealed class GetAccountTransactionsQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : IQueryHandler<GetAccountTransactionsQuery, List<AccountTransactionResponse>>
{
    /// <summary>Enough to reconcile a few months; older history lives on the transactions page.</summary>
    internal const int MaxRows = 100;

    public async Task<Result<List<AccountTransactionResponse>>> Handle(
        GetAccountTransactionsQuery query,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<List<AccountTransactionResponse>>(UserErrors.Unauthenticated);
        }

        Account? account = await dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == query.AccountId && a.UserId == userId, cancellationToken);
        if (account is null)
        {
            return Result.Failure<List<AccountTransactionResponse>>(AccountErrors.NotFound);
        }

        AccountSnapshot? latest = await dbContext.AccountSnapshots
            .Where(s => s.AccountId == account.Id)
            .OrderByDescending(s => s.Date)
            .ThenByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var rows = await dbContext.Transactions
            .Where(t => t.UserId == userId && (t.AccountId == account.Id || t.ToAccountId == account.Id))
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .Take(MaxRows)
            .Select(t => new
            {
                t.Id,
                t.Date,
                t.Content,
                Credit = t.Credit.Amount,
                Debit = t.Debit.Amount,
                t.PlanId,
                t.AccountId,
                t.ToAccountId,
                t.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r =>
            {
                var move = new AccountMove(r.AccountId, r.ToAccountId, r.Date, r.CreatedAt, r.Credit, r.Debit);
                // The same rule as the current balance, applied to this one row.
                AccountBalance alone = AccountBalances.Current(account, null, [move]);
                bool after = AccountBalances.Current(account, latest, [move]).TransactionsSinceSnapshot == 1;
                return new AccountTransactionResponse(
                    r.Id,
                    r.Date,
                    r.Content,
                    new MoneyResponse(r.Credit, Money.DefaultCurrency),
                    new MoneyResponse(r.Debit, Money.DefaultCurrency),
                    r.PlanId,
                    r.ToAccountId == account.Id,
                    new MoneyResponse(alone.Amount, Money.DefaultCurrency),
                    after);
            })
            .ToList();
    }
}
