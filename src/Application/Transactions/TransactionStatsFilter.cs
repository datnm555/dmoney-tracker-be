using System.Linq.Expressions;
using Application.Abstractions.Data;
using Domain.Transactions;

namespace Application.Transactions;

/// <summary>
/// The single "exclude from stats" rule: a transaction is left out of totals and charts
/// when its category OR its sub-category is flagged ExcludeFromStats. EF-translatable
/// (correlated EXISTS subqueries), so aggregations stay SQL-side.
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

    private static Expression<Func<Transaction, bool>> IsExcluded(IApplicationDbContext dbContext) =>
        t => dbContext.Categories.Any(c => c.Id == t.CategoryId && c.ExcludeFromStats)
             || dbContext.SubCategories.Any(s => s.Id == t.SubCategoryId && s.ExcludeFromStats);
}
