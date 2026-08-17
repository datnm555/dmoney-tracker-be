using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Plans;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Plans;

internal sealed class DeletePlanCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<DeletePlanCommand>
{
    public async Task<Result> Handle(DeletePlanCommand command, CancellationToken cancellationToken)
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
            return Result.Failure(PlanErrors.CannotDeleteDefault);
        }

        bool hasTransactions = await dbContext.Transactions
            .AnyAsync(t => t.PlanId == plan.Id, cancellationToken);
        if (hasTransactions)
        {
            return Result.Failure(PlanErrors.NotEmpty);
        }

        dbContext.Plans.Remove(plan);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
