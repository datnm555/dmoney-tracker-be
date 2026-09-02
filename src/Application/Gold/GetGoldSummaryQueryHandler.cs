using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Gold.Data;
using Application.Transactions.Data;
using Domain.Transactions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Gold;

internal sealed class GetGoldSummaryQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : IQueryHandler<GetGoldSummaryQuery, GoldSummaryResponse>
{
    public async Task<Result<GoldSummaryResponse>> Handle(
        GetGoldSummaryQuery query,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<GoldSummaryResponse>(UserErrors.Unauthenticated);
        }

        var rows = await dbContext.Transactions
            .Where(t => t.UserId == userId && t.GoldTypeId != null)
            .GroupBy(t => t.GoldTypeId)
            .Select(g => new
            {
                GoldTypeId = g.Key!.Value,
                BoughtQuantity = g.Sum(t => t.Debit.Amount > 0m ? t.GoldQuantity ?? 0m : 0m),
                SoldQuantity = g.Sum(t => t.Credit.Amount > 0m ? t.GoldQuantity ?? 0m : 0m),
                TotalSpent = g.Sum(t => t.Debit.Amount),
                TotalReceived = g.Sum(t => t.Credit.Amount)
            })
            .ToListAsync(cancellationToken);

        var typeRows = await dbContext.GoldTypes
            .Where(g => g.UserId == userId)
            .OrderBy(g => g.Name)
            .Select(g => new { g.Id, g.Name })
            .ToListAsync(cancellationToken);

        List<GoldTypeSummaryResponse> types = typeRows
            .Select(type =>
            {
                var row = rows.FirstOrDefault(r => r.GoldTypeId == type.Id);
                decimal bought = row?.BoughtQuantity ?? 0m;
                decimal sold = row?.SoldQuantity ?? 0m;
                decimal spent = row?.TotalSpent ?? 0m;
                decimal received = row?.TotalReceived ?? 0m;
                return new GoldTypeSummaryResponse(
                    type.Id,
                    type.Name,
                    bought - sold,
                    bought,
                    sold,
                    new MoneyResponse(spent, Money.DefaultCurrency),
                    new MoneyResponse(received, Money.DefaultCurrency),
                    new MoneyResponse(bought > 0m ? Math.Round(spent / bought, 2) : 0m, Money.DefaultCurrency));
            })
            .ToList();

        var txRows = await dbContext.Transactions
            .Where(t => t.UserId == userId && t.GoldTypeId != null)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id,
                t.Date,
                t.Content,
                GoldTypeId = t.GoldTypeId!.Value,
                GoldTypeName = dbContext.GoldTypes
                    .Where(g => g.Id == t.GoldTypeId)
                    .Select(g => g.Name)
                    .First(),
                GoldQuantity = t.GoldQuantity ?? 0m,
                CreditAmount = t.Credit.Amount,
                DebitAmount = t.Debit.Amount
            })
            .ToListAsync(cancellationToken);

        List<GoldTransactionResponse> transactions = txRows
            .Select(r => new GoldTransactionResponse(
                r.Id, r.Date, r.Content, r.GoldTypeId, r.GoldTypeName, r.GoldQuantity,
                new MoneyResponse(r.CreditAmount, Money.DefaultCurrency),
                new MoneyResponse(r.DebitAmount, Money.DefaultCurrency),
                new MoneyResponse(
                    r.GoldQuantity > 0m ? Math.Round((r.CreditAmount + r.DebitAmount) / r.GoldQuantity, 0) : 0m,
                    Money.DefaultCurrency)))
            .ToList();

        return new GoldSummaryResponse(types, transactions);
    }
}
