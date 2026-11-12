using Application.Abstractions.Data;
using Domain.Accounts;
using Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace Application.Accounts;

/// <summary>An account's balance right now and how many linked transactions moved it since the latest reading.</summary>
internal readonly record struct AccountBalance(decimal Amount, int TransactionsSinceSnapshot);

internal static class AccountBalances
{
    /// <summary>
    /// The latest reading plus every linked transaction recorded after it: dated later, or on the
    /// reading's day but entered after it. Earlier transactions are already in the reading. Debt types
    /// owe more on money out; money types hold more on money in. <paramref name="upTo"/> stops at a day,
    /// for a new reading dated then (later-dated transactions keep moving the balance after it).
    /// </summary>
    public static async Task<AccountBalance> CurrentAsync(
        IApplicationDbContext dbContext,
        Account account,
        AccountSnapshot? latest,
        CancellationToken cancellationToken,
        DateOnly? upTo = null)
    {
        IQueryable<Transaction> linked = dbContext.Transactions.Where(t => t.AccountId == account.Id);
        if (latest is not null)
        {
            DateOnly readingDate = latest.Date;
            DateTime readingCreatedAt = latest.CreatedAt;
            linked = linked.Where(t =>
                t.Date > readingDate || (t.Date == readingDate && t.CreatedAt > readingCreatedAt));
        }

        if (upTo is { } lastDay)
        {
            linked = linked.Where(t => t.Date <= lastDay);
        }

        var moves = await linked
            .Select(t => new { Credit = t.Credit.Amount, Debit = t.Debit.Amount })
            .ToListAsync(cancellationToken);

        bool isDebt = AccountTypes.IsDebt(account.Type);
        decimal net = moves.Sum(m => isDebt ? m.Debit - m.Credit : m.Credit - m.Debit);
        return new AccountBalance((latest?.Balance ?? 0) + net, moves.Count);
    }
}
