using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Accounts;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Accounts;

internal sealed class UpdateAccountCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<UpdateAccountCommand>
{
    public async Task<Result> Handle(UpdateAccountCommand command, CancellationToken cancellationToken)
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

        Result updated = account.Update(
            command.Name,
            command.Type,
            command.LastDigits,
            command.CreditLimit,
            command.StatementDay,
            command.DueDay,
            command.InterestRatePercent,
            command.BankName);
        if (updated.IsFailure)
        {
            return updated;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
