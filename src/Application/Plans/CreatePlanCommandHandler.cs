using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Plans;
using Domain.Users;
using SharedKernel;

namespace Application.Plans;

internal sealed class CreatePlanCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<CreatePlanCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        CreatePlanCommand command,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<Guid>(UserErrors.Unauthenticated);
        }

        Result<Plan> plan = Plan.Create(userId, command.Name);
        if (plan.IsFailure)
        {
            return Result.Failure<Guid>(plan.Error);
        }

        dbContext.Plans.Add(plan.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return plan.Value.Id;
    }
}
