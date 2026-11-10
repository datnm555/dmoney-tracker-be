using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Accounts;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Accounts;

internal sealed class DeleteAccountCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<DeleteAccountCommand>
{
    public async Task<Result> Handle(DeleteAccountCommand command, CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure(UserErrors.Unauthenticated);
        }

        Account? account = await dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == command.Id && a.UserId == userId, cancellationToken);
        if (account is null)
        {
            return Result.Failure(AccountErrors.NotFound);
        }

        // Snapshots are removed by the database cascade.
        dbContext.Accounts.Remove(account);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
