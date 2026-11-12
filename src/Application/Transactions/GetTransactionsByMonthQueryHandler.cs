using System.Globalization;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Transactions.Data;
using Domain.Plans;
using Domain.Transactions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Transactions;

internal sealed class GetTransactionsByMonthQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : IQueryHandler<GetTransactionsByMonthQuery, MonthlySummaryResponse>
{
    public async Task<Result<MonthlySummaryResponse>> Handle(
        GetTransactionsByMonthQuery query,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<MonthlySummaryResponse>(UserErrors.Unauthenticated);
        }

        bool planExists = await dbContext.Plans.AnyAsync(
            p => p.Id == query.PlanId && p.UserId == userId, cancellationToken);
        if (!planExists)
        {
            return Result.Failure<MonthlySummaryResponse>(PlanErrors.NotFound);
        }

        Result<(DateOnly Start, DateOnly EndExclusive)> range = query.From is not null || query.To is not null
            ? ParseDayRange(query.From, query.To)
            : ParseMonth(query.Month);
        if (range.IsFailure)
        {
            return Result.Failure<MonthlySummaryResponse>(range.Error);
        }

        (DateOnly rangeStart, DateOnly rangeEnd) = range.Value;

        IQueryable<Transaction> monthScope = dbContext.Transactions
            .Where(t => t.UserId == userId && t.PlanId == query.PlanId
                        && t.Date >= rangeStart && t.Date < rangeEnd);

        List<TransactionResponse> items = await monthScope
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .Select(t => new TransactionResponse(
                t.Id,
                t.Date,
                t.Content,
                new MoneyResponse(t.Credit.Amount, t.Credit.Currency),
                new MoneyResponse(t.Debit.Amount, t.Debit.Currency),
                t.Note,
                t.CategoryId,
                t.PaymentMethod,
                t.CardType,
                t.Bank,
                t.IsAdvance,
                dbContext.Transactions
                    .Where(a => a.ReimbursedByTransactionId == t.Id)
                    .Select(a => a.Id)
                    .ToList(),
                t.IsPrepaid,
                t.PrepaidFrom,
                t.PrepaidTo,
                t.PrepaidTransactionId,
                t.SubCategoryId,
                dbContext.SubCategories
                    .Where(s => s.Id == t.SubCategoryId)
                    .Select(s => s.Name)
                    .FirstOrDefault(),
                t.PlanId,
                t.ReimbursedByTransactionId,
                null,
                t.BeneficiaryId,
                dbContext.Beneficiaries
                    .Where(b => b.Id == t.BeneficiaryId)
                    .Select(b => b.Name)
                    .FirstOrDefault(),
                t.GoldTypeId,
                dbContext.GoldTypes
                    .Where(g => g.Id == t.GoldTypeId)
                    .Select(g => g.Name)
                    .FirstOrDefault(),
                t.GoldQuantity,
                t.PurchasePlaceId,
                dbContext.PurchasePlaces
                    .Where(p => p.Id == t.PurchasePlaceId)
                    .Select(p => p.Name)
                    .FirstOrDefault(),
                t.SavingsBank,
                false,
                t.AccountId))
            .ToListAsync(cancellationToken);

        items = await AttachLinksAsync(items, cancellationToken);
        items = await FlagExcludedAsync(monthScope, items, cancellationToken);

        // Totals skip excluded-from-stats rows; the items list above stays complete.
        IQueryable<Transaction> countedScope = monthScope.CountedInStats(dbContext);
        decimal totalCredit = await countedScope.SumAsync(t => t.Credit.Amount, cancellationToken);
        decimal totalDebit = await countedScope.SumAsync(t => t.Debit.Amount, cancellationToken);

        return new MonthlySummaryResponse(
            items,
            new MoneyResponse(totalCredit, Money.DefaultCurrency),
            new MoneyResponse(totalDebit, Money.DefaultCurrency),
            new MoneyResponse(totalCredit - totalDebit, Money.DefaultCurrency));
    }

    /// <summary>Longest inclusive day range a single request may ask for (a leap year).</summary>
    internal const int MaxRangeDays = 366;

    /// <summary>A bare year selects January through December; <c>YYYY-MM</c> selects that month.</summary>
    private static Result<(DateOnly Start, DateOnly EndExclusive)> ParseMonth(string month)
    {
        if (month.Length == 4
            && int.TryParse(month, NumberStyles.None, CultureInfo.InvariantCulture, out int year)
            && year is >= 1 and <= 9999)
        {
            var yearStart = new DateOnly(year, 1, 1);
            return (yearStart, yearStart.AddYears(1));
        }

        if (DateOnly.TryParseExact(
                month + "-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateOnly monthStart))
        {
            return (monthStart, monthStart.AddMonths(1));
        }

        return Result.Failure<(DateOnly, DateOnly)>(TransactionErrors.InvalidMonth);
    }

    /// <summary>Inclusive <c>from</c>..<c>to</c> day range; both ends are required.</summary>
    private static Result<(DateOnly Start, DateOnly EndExclusive)> ParseDayRange(string? from, string? to)
    {
        if (!TryParseDay(from, out DateOnly start) || !TryParseDay(to, out DateOnly end) || start > end)
        {
            return Result.Failure<(DateOnly, DateOnly)>(TransactionErrors.InvalidDateRange);
        }

        if (end.DayNumber - start.DayNumber + 1 > MaxRangeDays)
        {
            return Result.Failure<(DateOnly, DateOnly)>(TransactionErrors.DateRangeTooLong);
        }

        return (start, end.AddDays(1));
    }

    private static bool TryParseDay(string? value, out DateOnly day) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

    /// <summary>Marks items whose category or sub-category is excluded from stats.</summary>
    private async Task<List<TransactionResponse>> FlagExcludedAsync(
        IQueryable<Transaction> monthScope,
        List<TransactionResponse> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return items;
        }

        var excludedIds = (await monthScope
                .ExcludedFromStats(dbContext)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        return excludedIds.Count == 0
            ? items
            : items.Select(i => excludedIds.Contains(i.Id) ? i with { ExcludedFromStats = true } : i).ToList();
    }

    /// <summary>Attaches related transactions (advance/prepaid links, both directions).</summary>
    private async Task<List<TransactionResponse>> AttachLinksAsync(
        List<TransactionResponse> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return items;
        }

        List<Guid> itemIds = items.Select(i => i.Id).ToList();
        List<Guid> parentIds = items
            .SelectMany(i => new[] { i.ReimbursedByTransactionId, i.PrepaidTransactionId })
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var related = await dbContext.Transactions
            .Where(t => parentIds.Contains(t.Id)
                        || (t.ReimbursedByTransactionId != null && itemIds.Contains(t.ReimbursedByTransactionId.Value))
                        || (t.PrepaidTransactionId != null && itemIds.Contains(t.PrepaidTransactionId.Value)))
            .Select(t => new
            {
                t.Id,
                t.Date,
                t.Content,
                CreditAmount = t.Credit.Amount,
                CreditCurrency = t.Credit.Currency,
                DebitAmount = t.Debit.Amount,
                DebitCurrency = t.Debit.Currency,
                t.ReimbursedByTransactionId,
                t.PrepaidTransactionId
            })
            .ToListAsync(cancellationToken);

        LinkedTransactionResponse ToLink(dynamic r, string relation) => new(
            r.Id, r.Date, r.Content,
            new MoneyResponse(r.CreditAmount, r.CreditCurrency),
            new MoneyResponse(r.DebitAmount, r.DebitCurrency),
            relation);

        return items.Select(item =>
        {
            var links = new List<LinkedTransactionResponse>();

            // Credit that settled advances -> list them beneath it.
            links.AddRange(related
                .Where(r => r.ReimbursedByTransactionId == item.Id)
                .Select(r => ToLink(r, "reimburses")));

            // Advance already settled -> show the reimbursing credit.
            if (item.ReimbursedByTransactionId is { } by)
            {
                links.AddRange(related.Where(r => r.Id == by).Select(r => ToLink(r, "reimbursedBy")));
            }

            // Prepaid credit -> the expenses drawn from it.
            links.AddRange(related
                .Where(r => r.PrepaidTransactionId == item.Id)
                .Select(r => ToLink(r, "covers")));

            // Expense covered by a prepaid credit -> show the source.
            if (item.PrepaidTransactionId is { } prepaid)
            {
                links.AddRange(related.Where(r => r.Id == prepaid).Select(r => ToLink(r, "coveredBy")));
            }

            return links.Count > 0 ? item with { Links = links } : item;
        }).ToList();
    }
}
