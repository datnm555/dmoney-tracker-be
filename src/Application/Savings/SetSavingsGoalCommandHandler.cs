using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Savings;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Savings;

internal sealed class SetSavingsGoalCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<SetSavingsGoalCommand, Guid>
{
    public async Task<Result<Guid>> Handle(SetSavingsGoalCommand command, CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<Guid>(UserErrors.Unauthenticated);
        }

        string savingsBank = command.SavingsBank?.Trim() ?? string.Empty;
        SavingsGoal? existing = await dbContext.SavingsGoals
            .FirstOrDefaultAsync(g => g.UserId == userId && g.SavingsBank == savingsBank, cancellationToken);

        if (existing is not null)
        {
            Result updated = existing.Update(command.TargetAmount, command.StartPeriod, command.Months);
            if (updated.IsFailure)
            {
                return Result.Failure<Guid>(updated.Error);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return existing.Id;
        }

        Result<SavingsGoal> goal = SavingsGoal.Create(
            userId, savingsBank, command.TargetAmount, command.StartPeriod, command.Months);
        if (goal.IsFailure)
        {
            return Result.Failure<Guid>(goal.Error);
        }

        dbContext.SavingsGoals.Add(goal.Value);
        await dbContext.SaveChangesAsync(cancellationToken);
        return goal.Value.Id;
    }
}
