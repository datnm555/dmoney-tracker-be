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

internal sealed class GetAccountsQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : IQueryHandler<GetAccountsQuery, List<AccountResponse>>
{
    public async Task<Result<List<AccountResponse>>> Handle(
        GetAccountsQuery query,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<List<AccountResponse>>(UserErrors.Unauthenticated);
        }

        List<Account> accounts = await dbContext.Accounts
            .Where(a => a.UserId == userId)
            .ToListAsync(cancellationToken);

        var accountIds = accounts.Select(a => a.Id).ToList();
        // Only each account's newest reading, picked SQL-side (the history grows every month).
        List<AccountSnapshot> latestSnapshots = await dbContext.AccountSnapshots
            .Where(s => accountIds.Contains(s.AccountId))
            .GroupBy(s => s.AccountId)
            .Select(g => g.OrderByDescending(s => s.Date).ThenByDescending(s => s.CreatedAt).First())
            .ToListAsync(cancellationToken);
        var latest = latestSnapshots.ToDictionary(s => s.AccountId);

        List<AccountMove> moves = await LinkedMovesAsync(userId, accounts, accountIds, latest, cancellationToken);
        var balances = accounts.ToDictionary(
            a => a.Id,
            a => AccountBalances.Current(a, latest.GetValueOrDefault(a.Id), moves));

        // The type group order lives in AccountTypes.All, so sort in memory (a handful of rows per user).
        return accounts
            .OrderBy(a => IndexOfType(a.Type))
            .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(a => new AccountResponse(
                a.Id,
                a.Name,
                a.Type,
                a.LastDigits,
                a.CreditLimit is { } limit ? new MoneyResponse(limit, Money.DefaultCurrency) : null,
                a.StatementDay,
                a.DueDay,
                a.InterestRatePercent,
                latest.TryGetValue(a.Id, out AccountSnapshot? snapshot) ? snapshot.ToResponse() : null,
                a.BankName,
                a.IsDefault,
                new MoneyResponse(balances[a.Id].Amount, Money.DefaultCurrency),
                balances[a.Id].TransactionsSinceSnapshot,
                new MoneyResponse(balances[a.Id].PaidInSinceSnapshot, Money.DefaultCurrency),
                a.UntrackedDebt is { } debt ? new MoneyResponse(debt, Money.DefaultCurrency) : null,
                AccountBalances.UntrackedDebtRemaining(a, moves) is { } left
                    ? new MoneyResponse(left, Money.DefaultCurrency)
                    : null))
            .ToList();
    }

    /// <summary>
    /// One query for every transaction touching the accounts, from the oldest latest reading on
    /// (all of them when an account has no reading yet), plus any entered after a card's untracked
    /// debt was set, whatever their date.
    /// </summary>
    private async Task<List<AccountMove>> LinkedMovesAsync(
        Guid userId,
        List<Account> accounts,
        List<Guid> accountIds,
        Dictionary<Guid, AccountSnapshot> latest,
        CancellationToken cancellationToken)
    {
        if (accounts.Count == 0)
        {
            return [];
        }

        IQueryable<Transaction> linked = dbContext.Transactions.Where(t =>
            t.UserId == userId
            && ((t.AccountId != null && accountIds.Contains(t.AccountId.Value))
                || (t.ToAccountId != null && accountIds.Contains(t.ToAccountId.Value))));
        if (accounts.All(a => latest.ContainsKey(a.Id)))
        {
            DateOnly from = latest.Values.Min(s => s.Date);
            DateTime? debtSince = accounts.Min(a => a.UntrackedDebtSince);
            linked = debtSince is { } since
                ? linked.Where(t => t.Date >= from || t.CreatedAt > since)
                : linked.Where(t => t.Date >= from);
        }

        return await linked
            .Select(t => new AccountMove(t.AccountId, t.ToAccountId, t.Date, t.CreatedAt, t.Credit.Amount, t.Debit.Amount))
            .ToListAsync(cancellationToken);
    }

    private static int IndexOfType(string type)
    {
        for (int i = 0; i < AccountTypes.All.Count; i++)
        {
            if (string.Equals(AccountTypes.All[i], type, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return int.MaxValue;
    }
}
