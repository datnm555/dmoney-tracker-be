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
        List<AccountSnapshot> snapshots = await dbContext.AccountSnapshots
            .Where(s => accountIds.Contains(s.AccountId))
            .ToListAsync(cancellationToken);
        Dictionary<Guid, AccountSnapshot> latest = snapshots
            .GroupBy(s => s.AccountId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(s => s.Date).ThenByDescending(s => s.CreatedAt).First());

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
                a.IsDefault))
            .ToList();
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
