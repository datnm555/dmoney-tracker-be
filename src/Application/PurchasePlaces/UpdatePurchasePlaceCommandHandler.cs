using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.PurchasePlaces;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.PurchasePlaces;

internal sealed class UpdatePurchasePlaceCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<UpdatePurchasePlaceCommand>
{
    public async Task<Result> Handle(UpdatePurchasePlaceCommand command, CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure(UserErrors.Unauthenticated);
        }

        PurchasePlace? purchasePlace = await dbContext.PurchasePlaces
            .FirstOrDefaultAsync(p => p.Id == command.Id && p.UserId == userId, cancellationToken);
        if (purchasePlace is null)
        {
            return Result.Failure(PurchasePlaceErrors.NotFound);
        }

        Result rename = purchasePlace.Rename(command.Name);
        if (rename.IsFailure)
        {
            return rename;
        }

        bool isDuplicate = await dbContext.PurchasePlaces
            .AnyAsync(p => p.UserId == userId && p.Name == purchasePlace.Name && p.Id != command.Id, cancellationToken);
        if (isDuplicate)
        {
            return Result.Failure(PurchasePlaceErrors.Duplicate);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
