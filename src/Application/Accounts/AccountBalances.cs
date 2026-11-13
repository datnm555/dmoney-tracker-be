using Domain.Accounts;

namespace Application.Accounts;

/// <summary>
/// An account's balance right now, how many linked transactions moved it since the latest reading,
/// and how much was paid into it since then (for a card: paid towards the statement on that reading).
/// </summary>
internal readonly record struct AccountBalance(decimal Amount, int TransactionsSinceSnapshot, decimal PaidInSinceSnapshot);

/// <summary>One transaction touching the owner's accounts, as far as balances care.</summary>
internal readonly record struct AccountMove(
    Guid? AccountId, Guid? ToAccountId, DateOnly Date, DateTime CreatedAt, decimal Credit, decimal Debit);

internal static class AccountBalances
{
    /// <summary>
    /// The latest reading plus every linked transaction recorded after it: dated later, or on the
    /// reading's day but entered after it. Earlier transactions are already in the reading. Debt types
    /// owe more on money out; money types hold more on money in. A transfer into the account (its
    /// ToAccountId) moves it the opposite way: paying a card owes less. Nothing is ever written back,
    /// so editing or deleting a transaction always moves the balance back.
    /// </summary>
    public static AccountBalance Current(Account account, AccountSnapshot? latest, IEnumerable<AccountMove> moves)
    {
        bool isDebt = AccountTypes.IsDebt(account.Type);
        decimal net = 0m;
        decimal paidIn = 0m;
        int count = 0;
        foreach (AccountMove move in moves)
        {
            bool incoming = move.ToAccountId == account.Id;
            if ((!incoming && move.AccountId != account.Id) || !IsAfter(move, latest))
            {
                continue;
            }

            decimal outgoing = isDebt ? move.Debit - move.Credit : move.Credit - move.Debit;
            net += incoming ? -outgoing : outgoing;
            if (incoming)
            {
                paidIn += move.Debit;
            }

            count++;
        }

        return new AccountBalance((latest?.Balance ?? 0m) + net, count, paidIn);
    }

    /// <summary>
    /// What is left of a card's untracked debt: every payment into the card entered after the debt was
    /// last set pays it off first. Null when the account carries none.
    /// </summary>
    public static decimal? UntrackedDebtRemaining(Account account, IEnumerable<AccountMove> moves)
    {
        if (account.UntrackedDebt is not { } debt || account.UntrackedDebtSince is not { } since)
        {
            return null;
        }

        decimal paid = moves
            .Where(m => m.ToAccountId == account.Id && m.CreatedAt > since)
            .Sum(m => m.Debit);
        return Math.Max(0m, debt - paid);
    }

    private static bool IsAfter(AccountMove move, AccountSnapshot? latest) =>
        latest is null
        || move.Date > latest.Date
        || (move.Date == latest.Date && move.CreatedAt > latest.CreatedAt);
}
