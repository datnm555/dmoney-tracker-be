using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Plans;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Plans;

internal sealed class SetDefaultPlanCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<SetDefaultPlanCommand>
{
    public async Task<Result> Handle(SetDefaultPlanCommand command, CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure(UserErrors.Unauthenticated);
        }

        Plan? plan = await dbContext.Plans
            .FirstOrDefaultAsync(p => p.Id == command.Id && p.UserId == userId, cancellationToken);
        if (plan is null)
        {
            return Result.Failure(PlanErrors.NotFound);
        }

        if (plan.IsDefault)
        {
            return Result.Success();
        }

        List<Plan> currentDefaults = await dbContext.Plans
            .Where(p => p.UserId == userId && p.IsDefault)
            .ToListAsync(cancellationToken);
        foreach (Plan current in currentDefaults)
        {
            current.ClearDefault();
        }

        plan.MakeDefault();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
