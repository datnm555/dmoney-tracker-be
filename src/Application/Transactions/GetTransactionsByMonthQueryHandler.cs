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

        DateOnly rangeStart;
        DateOnly rangeEnd;
        if (query.Month.Length == 4
            && int.TryParse(query.Month, NumberStyles.None, CultureInfo.InvariantCulture, out int year)
            && year is >= 1 and <= 9999)
        {
            // A bare year selects January through December of that year.
            rangeStart = new DateOnly(year, 1, 1);
            rangeEnd = rangeStart.AddYears(1);
        }
        else if (DateOnly.TryParseExact(
                query.Month + "-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateOnly monthStart))
        {
            rangeStart = monthStart;
            rangeEnd = monthStart.AddMonths(1);
        }
        else
        {
            return Result.Failure<MonthlySummaryResponse>(TransactionErrors.InvalidMonth);
        }

        IQueryable<Transaction> monthScope = dbContext.Transactions
            .Where(t => t.UserId == userId && t.Date >= rangeStart && t.Date < rangeEnd);

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
                t.ReimbursedByTransactionId))
            .ToListAsync(cancellationToken);

        items = await AttachLinksAsync(items, cancellationToken);

        decimal totalCredit = await monthScope.SumAsync(t => t.Credit.Amount, cancellationToken);
        decimal totalDebit = await monthScope.SumAsync(t => t.Debit.Amount, cancellationToken);

        return new MonthlySummaryResponse(
            items,
            new MoneyResponse(totalCredit, Money.DefaultCurrency),
            new MoneyResponse(totalDebit, Money.DefaultCurrency),
            new MoneyResponse(totalCredit - totalDebit, Money.DefaultCurrency));
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
