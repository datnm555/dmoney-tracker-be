using System.Globalization;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Transactions.Data;
using Domain.Transactions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Transactions;

internal sealed class GetDashboardStatsQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext,
    IDateTimeProvider clock)
    : IQueryHandler<GetDashboardStatsQuery, DashboardStatsResponse>
{
    public async Task<Result<DashboardStatsResponse>> Handle(
        GetDashboardStatsQuery query,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<DashboardStatsResponse>(UserErrors.Unauthenticated);
        }

        if (!DateOnly.TryParseExact(
                query.Month + "-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateOnly monthStart))
        {
            return Result.Failure<DashboardStatsResponse>(TransactionErrors.InvalidMonth);
        }

        DateOnly nextMonthStart = monthStart.AddMonths(1);

        List<MonthlyStatResponse> monthly = await BuildMonthlyAsync(userId, cancellationToken);
        List<DailyStatResponse> daily = await BuildDailyAsync(userId, monthStart, nextMonthStart, cancellationToken);
        List<CategoryStatResponse> byCategory = await BuildByCategoryAsync(userId, monthStart, nextMonthStart, cancellationToken);

        return new DashboardStatsResponse(monthly, daily, byCategory);
    }

    private async Task<List<MonthlyStatResponse>> BuildMonthlyAsync(Guid userId, CancellationToken cancellationToken)
    {
        DateOnly today = DateOnly.FromDateTime(clock.UtcNow);
        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
        DateOnly windowStart = currentMonthStart.AddMonths(-11);
        DateOnly windowEnd = currentMonthStart.AddMonths(1);

        var rows = await dbContext.Transactions
            .Where(t => t.UserId == userId && t.Date >= windowStart && t.Date < windowEnd)
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Credit = g.Sum(t => t.Credit.Amount),
                Debit = g.Sum(t => t.Debit.Amount)
            })
            .ToListAsync(cancellationToken);

        List<MonthlyStatResponse> monthly = [];
        for (int i = 0; i < 12; i++)
        {
            DateOnly slot = windowStart.AddMonths(i);
            var row = rows.FirstOrDefault(r => r.Year == slot.Year && r.Month == slot.Month);
            decimal credit = row?.Credit ?? 0m;
            decimal debit = row?.Debit ?? 0m;
            monthly.Add(new MonthlyStatResponse(
                slot.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                new MoneyResponse(credit, Money.DefaultCurrency),
                new MoneyResponse(debit, Money.DefaultCurrency),
                new MoneyResponse(credit - debit, Money.DefaultCurrency)));
        }

        return monthly;
    }

    private async Task<List<DailyStatResponse>> BuildDailyAsync(
        Guid userId, DateOnly monthStart, DateOnly nextMonthStart, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Transactions
            .Where(t => t.UserId == userId && t.Date >= monthStart && t.Date < nextMonthStart)
            .GroupBy(t => t.Date.Day)
            .Select(g => new { Day = g.Key, Debit = g.Sum(t => t.Debit.Amount) })
            .ToListAsync(cancellationToken);

        return rows
            .Where(r => r.Debit > 0m)
            .OrderBy(r => r.Day)
            .Select(r => new DailyStatResponse(r.Day, new MoneyResponse(r.Debit, Money.DefaultCurrency)))
            .ToList();
    }

    private async Task<List<CategoryStatResponse>> BuildByCategoryAsync(
        Guid userId, DateOnly monthStart, DateOnly nextMonthStart, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Transactions
            .Where(t => t.UserId == userId
                        && t.Date >= monthStart
                        && t.Date < nextMonthStart
                        && t.Debit.Amount > 0m)
            .GroupBy(t => t.CategoryId)
            .Select(g => new { CategoryId = g.Key, Debit = g.Sum(t => t.Debit.Amount) })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new CategoryStatResponse(
                r.CategoryId,
                new MoneyResponse(r.Debit, Money.DefaultCurrency)))
            .OrderByDescending(c => c.Debit.Amount)
            .ToList();
    }
}
