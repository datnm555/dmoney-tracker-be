using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.GoldAcquisitions.Data;
using Application.Transactions.Data;
using Domain.Transactions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.GoldAcquisitions;

internal sealed class GetGoldAcquisitionsQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : IQueryHandler<GetGoldAcquisitionsQuery, List<GoldAcquisitionResponse>>
{
    public async Task<Result<List<GoldAcquisitionResponse>>> Handle(
        GetGoldAcquisitionsQuery query,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<List<GoldAcquisitionResponse>>(UserErrors.Unauthenticated);
        }

        return await dbContext.GoldAcquisitions
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
    }
}
