using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Plans.Data;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Plans;

internal sealed class GetPlansQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : IQueryHandler<GetPlansQuery, List<PlanResponse>>
{
    public async Task<Result<List<PlanResponse>>> Handle(
        GetPlansQuery query,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<List<PlanResponse>>(UserErrors.Unauthenticated);
        }

        return await dbContext.Plans
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.IsDefault)
            .ThenBy(p => p.Name)
            .Select(p => new PlanResponse(p.Id, p.Name, p.IsDefault))
            .ToListAsync(cancellationToken);
    }
}
