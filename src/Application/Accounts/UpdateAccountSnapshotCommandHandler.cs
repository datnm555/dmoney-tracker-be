using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Accounts;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Accounts;

internal sealed class UpdateAccountSnapshotCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<UpdateAccountSnapshotCommand>
{
    public async Task<Result> Handle(UpdateAccountSnapshotCommand command, CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure(UserErrors.Unauthenticated);
        }

        AccountSnapshot? snapshot = await dbContext.AccountSnapshots
            .FirstOrDefaultAsync(
                s => s.Id == command.Id && dbContext.Accounts.Any(a => a.Id == s.AccountId && a.UserId == userId),
                cancellationToken);
        if (snapshot is null)
        {
            return Result.Failure(AccountErrors.SnapshotNotFound);
        }

        Result updated = snapshot.Update(
            command.Date,
            command.Balance,
            command.StatementAmount,
            command.DueDate,
            command.InstallmentRemaining,
            command.StatementPaid,
            command.Note,
            command.StatementUntracked);
        if (updated.IsFailure)
        {
            return updated;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
