using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Banks;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Banks;

internal sealed class SetDefaultBankCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<SetDefaultBankCommand>
{
    public async Task<Result> Handle(SetDefaultBankCommand command, CancellationToken cancellationToken)
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

        if (bank.IsDefault)
        {
            return Result.Success();
        }

        List<Bank> currentDefaults = await dbContext.Banks
            .Where(b => b.UserId == userId && b.IsDefault)
            .ToListAsync(cancellationToken);
        foreach (Bank current in currentDefaults)
        {
            current.ClearDefault();
        }

        bank.MakeDefault();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
