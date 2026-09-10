using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Banks;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Banks;

internal sealed class DeleteBankCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<DeleteBankCommand>
{
    public async Task<Result> Handle(DeleteBankCommand command, CancellationToken cancellationToken)
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

        // No InUse guard: transactions store the bank as free text, so old rows
        // are untouched by removing a catalog entry.
        dbContext.Banks.Remove(bank);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
