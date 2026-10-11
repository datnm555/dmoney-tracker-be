using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Savings.Data;
using Application.Transactions.Data;
using Domain.Transactions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Savings;

/// <summary>
/// Savings = transactions whose category code is "savings". Debit = deposit, credit = withdrawal.
/// Grouped per SavingsBank ("Nơi gửi"); rows without one form a single unassigned (null) bucket.
/// </summary>
internal sealed class GetSavingsSummaryQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : IQueryHandler<GetSavingsSummaryQuery, SavingsSummaryResponse>
{
    private const string SavingsCategoryCode = "savings";

    public async Task<Result<SavingsSummaryResponse>> Handle(
        GetSavingsSummaryQuery query,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<SavingsSummaryResponse>(UserErrors.Unauthenticated);
        }

        IQueryable<Guid?> savingsCategoryIds = dbContext.Categories
            .Where(c => c.Code == SavingsCategoryCode)
            .Select(c => (Guid?)c.Id);

        IQueryable<Transaction> scope = dbContext.Transactions
            .Where(t => t.UserId == userId && savingsCategoryIds.Contains(t.CategoryId));

        var accountRows = await scope
            .GroupBy(t => t.SavingsBank)
            .Select(g => new
            {
                SavingsBank = g.Key,
                Deposited = g.Sum(t => t.Debit.Amount),
                Withdrawn = g.Sum(t => t.Credit.Amount),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        List<SavingsAccountResponse> accounts = accountRows
            .Select(r => new
            {
                r.SavingsBank,
                r.Deposited,
                r.Withdrawn,
                r.Count,
                Balance = r.Deposited - r.Withdrawn
            })
            .OrderBy(r => r.SavingsBank is null)
            .ThenByDescending(r => r.Balance)
            .ThenBy(r => r.SavingsBank, StringComparer.Ordinal)
            .Select(r => new SavingsAccountResponse(
                r.SavingsBank,
                new MoneyResponse(r.Balance, Money.DefaultCurrency),
                new MoneyResponse(r.Deposited, Money.DefaultCurrency),
                new MoneyResponse(r.Withdrawn, Money.DefaultCurrency),
                r.Count))
            .ToList();

        decimal total = accounts.Sum(a => a.Balance.Amount);

        List<SavingsTransactionResponse> transactions = await scope
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .Select(t => new SavingsTransactionResponse(
                t.Id,
                t.Date,
                t.Content,
                t.SavingsBank,
                t.Bank,
                new MoneyResponse(t.Credit.Amount, Money.DefaultCurrency),
                new MoneyResponse(t.Debit.Amount, Money.DefaultCurrency),
                t.PlanId,
                dbContext.SubCategories
                    .Where(s => s.Id == t.SubCategoryId)
                    .Select(s => s.Name)
                    .FirstOrDefault(),
                t.Note))
            .ToListAsync(cancellationToken);

        return new SavingsSummaryResponse(
            new MoneyResponse(total, Money.DefaultCurrency),
            accounts,
            transactions);
    }
}
