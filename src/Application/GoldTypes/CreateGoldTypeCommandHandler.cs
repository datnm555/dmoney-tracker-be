using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.GoldTypes;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.GoldTypes;

internal sealed class CreateGoldTypeCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<CreateGoldTypeCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        CreateGoldTypeCommand command,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<Guid>(UserErrors.Unauthenticated);
        }

        Result<GoldType> goldType = GoldType.Create(userId, command.Name);
        if (goldType.IsFailure)
        {
            return Result.Failure<Guid>(goldType.Error);
        }

        bool duplicate = await dbContext.GoldTypes.AnyAsync(
            g => g.UserId == userId && g.Name == goldType.Value.Name, cancellationToken);
        if (duplicate)
        {
            return Result.Failure<Guid>(GoldTypeErrors.Duplicate);
        }

        dbContext.GoldTypes.Add(goldType.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return goldType.Value.Id;
    }
}
