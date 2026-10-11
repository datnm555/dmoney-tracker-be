using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Savings.Data;
using Application.Transactions.Data;
using Domain.Plans;
using Domain.SubCategories;
using Domain.Transactions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Savings;

/// <summary>
/// Savings = the user's default-plan transactions whose sub-category code is "savings_book"
/// ("Tích luỹ › Sổ tiết kiệm"; gold under Tích luỹ is excluded). Debit = deposit, credit = withdrawal.
/// Grouped per SavingsBank ("Nơi gửi"); rows without one form a single unassigned (null) bucket.
/// </summary>
internal sealed class GetSavingsSummaryQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : IQueryHandler<GetSavingsSummaryQuery, SavingsSummaryResponse>
{
    public async Task<Result<SavingsSummaryResponse>> Handle(
        GetSavingsSummaryQuery query,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<SavingsSummaryResponse>(UserErrors.Unauthenticated);
        }

        var plan = await dbContext.Plans
            .Where(p => p.UserId == userId && p.IsDefault)
            .Select(p => new { p.Id, p.Name })
            .FirstOrDefaultAsync(cancellationToken);
        if (plan is null)
        {
            return Result.Failure<SavingsSummaryResponse>(PlanErrors.NotFound);
        }

        IQueryable<Guid?> savingsBookIds = dbContext.SubCategories
            .Where(s => s.Code == SubCategoryCodes.SavingsBook)
            .Select(s => (Guid?)s.Id);

        IQueryable<Transaction> scope = dbContext.Transactions
            .Where(t => t.UserId == userId
                        && t.PlanId == plan.Id
                        && savingsBookIds.Contains(t.SubCategoryId));

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
            plan.Id,
            plan.Name,
            new MoneyResponse(total, Money.DefaultCurrency),
            accounts,
            transactions);
    }
}
