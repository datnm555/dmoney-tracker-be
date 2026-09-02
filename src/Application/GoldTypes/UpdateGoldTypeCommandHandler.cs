using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.GoldTypes;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.GoldTypes;

internal sealed class UpdateGoldTypeCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<UpdateGoldTypeCommand>
{
    public async Task<Result> Handle(UpdateGoldTypeCommand command, CancellationToken cancellationToken)
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

        Result rename = goldType.Rename(command.Name);
        if (rename.IsFailure)
        {
            return rename;
        }

        bool isDuplicate = await dbContext.GoldTypes
            .AnyAsync(g => g.UserId == userId && g.Name == goldType.Name && g.Id != command.Id, cancellationToken);
        if (isDuplicate)
        {
            return Result.Failure(GoldTypeErrors.Duplicate);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
