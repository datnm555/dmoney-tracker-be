using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Plans;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Plans;

internal sealed class UpdatePlanCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<UpdatePlanCommand>
{
    public async Task<Result> Handle(UpdatePlanCommand command, CancellationToken cancellationToken)
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

        Result rename = plan.Rename(command.Name);
        if (rename.IsFailure)
        {
            return rename;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
