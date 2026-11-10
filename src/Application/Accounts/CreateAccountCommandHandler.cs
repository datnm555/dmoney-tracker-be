using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Accounts;
using Domain.Users;
using SharedKernel;

namespace Application.Accounts;

internal sealed class CreateAccountCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<CreateAccountCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateAccountCommand command, CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<Guid>(UserErrors.Unauthenticated);
        }

        Result<Account> account = Account.Create(
            userId,
            command.Name,
            command.Type,
            command.LastDigits,
            command.CreditLimit,
            command.StatementDay,
            command.DueDay,
            command.InterestRatePercent);
        if (account.IsFailure)
        {
            return Result.Failure<Guid>(account.Error);
        }

        dbContext.Accounts.Add(account.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return account.Value.Id;
    }
}
