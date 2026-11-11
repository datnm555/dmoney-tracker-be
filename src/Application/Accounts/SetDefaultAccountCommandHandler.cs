using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Accounts;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Accounts;

internal sealed class SetDefaultAccountCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<SetDefaultAccountCommand>
{
    public async Task<Result> Handle(SetDefaultAccountCommand command, CancellationToken cancellationToken)
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

        if (account.IsDefault)
        {
            return Result.Success();
        }

        List<Account> currentDefaults = await dbContext.Accounts
            .Where(a => a.UserId == userId && a.IsDefault)
            .ToListAsync(cancellationToken);
        foreach (Account current in currentDefaults)
        {
            current.ClearDefault();
        }

        account.MakeDefault();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
