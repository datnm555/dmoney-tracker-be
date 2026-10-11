using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Banks;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Banks;

internal sealed class UpdateBankCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<UpdateBankCommand>
{
    public async Task<Result> Handle(UpdateBankCommand command, CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure(UserErrors.Unauthenticated);
        }

        Bank? bank = await dbContext.Banks
            .FirstOrDefaultAsync(b => b.Id == command.Id && b.UserId == userId, cancellationToken);
        if (bank is null)
        {
            return Result.Failure(BankErrors.NotFound);
        }

        Result rename = bank.Rename(command.Name);
        if (rename.IsFailure)
        {
            return rename;
        }

        bool isDuplicate = await dbContext.Banks
            .AnyAsync(b => b.UserId == userId && b.Name == bank.Name && b.Id != command.Id, cancellationToken);
        if (isDuplicate)
        {
            return Result.Failure(BankErrors.Duplicate);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
