using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Banks;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Banks;

internal sealed class CreateBankCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<CreateBankCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateBankCommand command, CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<Guid>(UserErrors.Unauthenticated);
        }

        Result<Bank> bank = Bank.Create(userId, command.Name);
        if (bank.IsFailure)
        {
            return Result.Failure<Guid>(bank.Error);
        }

        bool duplicate = await dbContext.Banks.AnyAsync(
            b => b.UserId == userId && b.Name == bank.Value.Name, cancellationToken);
        if (duplicate)
        {
            return Result.Failure<Guid>(BankErrors.Duplicate);
        }

        dbContext.Banks.Add(bank.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return bank.Value.Id;
    }
}
