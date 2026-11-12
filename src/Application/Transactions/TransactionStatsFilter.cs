using System.Linq.Expressions;
using Application.Abstractions.Data;
using Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace Application.Transactions;

/// <summary>Part of a counted credit that only repaid the owner's advances, so is not income.</summary>
internal readonly record struct ReimbursementOffset(Guid TransactionId, DateOnly Date, decimal Amount);

/// <summary>
/// The single "exclude from stats" rule. A transaction is left out of income/expense totals and
/// charts when:
/// - its category OR its sub-category is flagged ExcludeFromStats (internal transfers, savings, loans);
/// - it is an advance paid on someone else's behalf (not the owner's spending);
/// - it buys or sells gold (money turned into an asset and back).
/// A credit that reimburses advances counts only what exceeds them (<see cref="ReimbursementOffsetsAsync"/>).
/// EF-translatable (correlated EXISTS subqueries), so aggregations stay SQL-side.
/// </summary>
internal static class TransactionStatsFilter
{
    /// <summary>Transactions that count towards totals and charts.</summary>
    internal static IQueryable<Transaction> CountedInStats(
        this IQueryable<Transaction> source, IApplicationDbContext dbContext)
    {
        Expression<Func<Transaction, bool>> excluded = IsExcluded(dbContext);
        var counted = Expression.Lambda<Func<Transaction, bool>>(
            Expression.Not(excluded.Body), excluded.Parameters);
        return source.Where(counted);
    }

    /// <summary>Transactions left out of totals and charts.</summary>
    internal static IQueryable<Transaction> ExcludedFromStats(
        this IQueryable<Transaction> source, IApplicationDbContext dbContext) =>
        source.Where(IsExcluded(dbContext));

    /// <summary>
    /// For each counted credit that reimbursed advances: the reimbursed part (capped at the credit),
    /// to subtract from income. Only reimbursing rows come back, so the list stays small.
    /// </summary>
    internal static async Task<List<ReimbursementOffset>> ReimbursementOffsetsAsync(
        this IQueryable<Transaction> counted, IApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var rows = await counted
            .Where(t => t.Credit.Amount > 0m
                        && dbContext.Transactions.Any(a => a.ReimbursedByTransactionId == t.Id))
            .Select(t => new
            {
                t.Id,
                t.Date,
                Credit = t.Credit.Amount,
                Repaid = dbContext.Transactions
                    .Where(a => a.ReimbursedByTransactionId == t.Id)
                    .Sum(a => a.Debit.Amount)
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new ReimbursementOffset(r.Id, r.Date, Math.Min(r.Credit, r.Repaid)))
            .Where(o => o.Amount > 0m)
            .ToList();
    }

    private static Expression<Func<Transaction, bool>> IsExcluded(IApplicationDbContext dbContext) =>
        t => t.IsAdvance
             || t.GoldTypeId != null
             || dbContext.Categories.Any(c => c.Id == t.CategoryId && c.ExcludeFromStats)
             || dbContext.SubCategories.Any(s => s.Id == t.SubCategoryId && s.ExcludeFromStats);
}
