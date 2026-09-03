using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.PurchasePlaces.Data;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.PurchasePlaces;

internal sealed class GetPurchasePlacesQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : IQueryHandler<GetPurchasePlacesQuery, List<PurchasePlaceResponse>>
{
    public async Task<Result<List<PurchasePlaceResponse>>> Handle(
        GetPurchasePlacesQuery query,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<List<PurchasePlaceResponse>>(UserErrors.Unauthenticated);
        }

        return await dbContext.PurchasePlaces
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.Name)
            .Select(p => new PurchasePlaceResponse(p.Id, p.Name))
            .ToListAsync(cancellationToken);
    }
}
