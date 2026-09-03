using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Gold.Data;
using Application.GoldAcquisitions.Data;
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

        var acqRows = await dbContext.GoldAcquisitions
            .Where(a => a.UserId == userId)
            .GroupBy(a => a.GoldTypeId)
            .Select(g => new
            {
                GoldTypeId = g.Key,
                Quantity = g.Sum(a => a.Quantity),
                Cost = g.Sum(a => a.Quantity * a.UnitPrice)
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
                decimal txBought = row?.BoughtQuantity ?? 0m;
                decimal sold = row?.SoldQuantity ?? 0m;
                decimal txSpent = row?.TotalSpent ?? 0m;
                decimal received = row?.TotalReceived ?? 0m;

                decimal acqQty = acqRows.FirstOrDefault(r => r.GoldTypeId == type.Id)?.Quantity ?? 0m;
                decimal acqCost = acqRows.FirstOrDefault(r => r.GoldTypeId == type.Id)?.Cost ?? 0m;

                decimal bought = txBought + acqQty;
                decimal spent = txSpent + acqCost;
                decimal held = bought - sold;

                return new GoldTypeSummaryResponse(
                    type.Id,
                    type.Name,
                    held,
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
                DebitAmount = t.Debit.Amount,
                t.PurchasePlaceId,
                PurchasePlaceName = dbContext.PurchasePlaces
                    .Where(p => p.Id == t.PurchasePlaceId)
                    .Select(p => p.Name)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        List<GoldTransactionResponse> transactions = txRows
            .Select(r => new GoldTransactionResponse(
                r.Id, r.Date, r.Content, r.GoldTypeId, r.GoldTypeName, r.GoldQuantity,
                new MoneyResponse(r.CreditAmount, Money.DefaultCurrency),
                new MoneyResponse(r.DebitAmount, Money.DefaultCurrency),
                new MoneyResponse(
                    r.GoldQuantity > 0m ? Math.Round((r.CreditAmount + r.DebitAmount) / r.GoldQuantity, 0) : 0m,
                    Money.DefaultCurrency),
                r.PurchasePlaceId,
                r.PurchasePlaceName))
            .ToList();

        List<GoldAcquisitionResponse> acquisitions = await dbContext.GoldAcquisitions
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Date)
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => new GoldAcquisitionResponse(
                a.Id,
                a.Date,
                a.GoldTypeId,
                dbContext.GoldTypes.Where(g => g.Id == a.GoldTypeId).Select(g => g.Name).First(),
                a.Quantity,
                new MoneyResponse(a.UnitPrice, Money.DefaultCurrency),
                new MoneyResponse(Math.Round(a.Quantity * a.UnitPrice, 0), Money.DefaultCurrency),
                a.Note,
                a.PurchasePlaceId,
                dbContext.PurchasePlaces
                    .Where(p => p.Id == a.PurchasePlaceId)
                    .Select(p => p.Name)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return new GoldSummaryResponse(types, transactions, acquisitions);
    }
}
