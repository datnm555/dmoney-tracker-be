using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Accounts;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Accounts;

internal sealed class CreateAccountSnapshotCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<CreateAccountSnapshotCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateAccountSnapshotCommand command, CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<Guid>(UserErrors.Unauthenticated);
        }

        bool owned = await dbContext.Accounts
            .AnyAsync(a => a.Id == command.AccountId && a.UserId == userId, cancellationToken);
        if (!owned)
        {
            return Result.Failure<Guid>(AccountErrors.NotFound);
        }

        Result<AccountSnapshot> snapshot = AccountSnapshot.Create(
            command.AccountId,
            command.Date,
            command.Balance,
            command.StatementAmount,
            command.DueDate,
            command.InstallmentRemaining,
            command.StatementPaid,
            command.Note);
        if (snapshot.IsFailure)
        {
            return Result.Failure<Guid>(snapshot.Error);
        }

        dbContext.AccountSnapshots.Add(snapshot.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return snapshot.Value.Id;
    }
}
