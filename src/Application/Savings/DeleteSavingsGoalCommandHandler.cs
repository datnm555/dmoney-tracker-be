using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Savings;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Savings;

internal sealed class DeleteSavingsGoalCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<DeleteSavingsGoalCommand>
{
    public async Task<Result> Handle(DeleteSavingsGoalCommand command, CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure(UserErrors.Unauthenticated);
        }

        SavingsGoal? goal = await dbContext.SavingsGoals
            .FirstOrDefaultAsync(g => g.Id == command.Id && g.UserId == userId, cancellationToken);
        if (goal is null)
        {
            return Result.Failure(SavingsGoalErrors.NotFound);
        }

        dbContext.SavingsGoals.Remove(goal);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
