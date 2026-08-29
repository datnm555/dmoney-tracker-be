using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.GoldTypes;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.GoldTypes;

internal sealed class DeleteGoldTypeCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<DeleteGoldTypeCommand>
{
    public async Task<Result> Handle(DeleteGoldTypeCommand command, CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure(UserErrors.Unauthenticated);
        }

        GoldType? goldType = await dbContext.GoldTypes
            .FirstOrDefaultAsync(g => g.Id == command.Id && g.UserId == userId, cancellationToken);
        if (goldType is null)
        {
            return Result.Failure(GoldTypeErrors.NotFound);
        }

        bool inUse = await dbContext.Transactions.AnyAsync(
            t => t.GoldTypeId == goldType.Id, cancellationToken);
        if (inUse)
        {
            return Result.Failure(GoldTypeErrors.InUse);
        }

        dbContext.GoldTypes.Remove(goldType);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
