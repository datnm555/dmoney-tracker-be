using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.GoldTypes.Data;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.GoldTypes;

internal sealed class GetGoldTypesQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : IQueryHandler<GetGoldTypesQuery, List<GoldTypeResponse>>
{
    public async Task<Result<List<GoldTypeResponse>>> Handle(
        GetGoldTypesQuery query,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<List<GoldTypeResponse>>(UserErrors.Unauthenticated);
        }

        return await dbContext.GoldTypes
            .Where(g => g.UserId == userId)
            .OrderBy(g => g.Name)
            .Select(g => new GoldTypeResponse(g.Id, g.Name))
            .ToListAsync(cancellationToken);
    }
}
