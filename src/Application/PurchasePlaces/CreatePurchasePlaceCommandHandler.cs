using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.PurchasePlaces;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.PurchasePlaces;

internal sealed class CreatePurchasePlaceCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<CreatePurchasePlaceCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        CreatePurchasePlaceCommand command,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<Guid>(UserErrors.Unauthenticated);
        }

        Result<PurchasePlace> purchasePlace = PurchasePlace.Create(userId, command.Name);
        if (purchasePlace.IsFailure)
        {
            return Result.Failure<Guid>(purchasePlace.Error);
        }

        bool duplicate = await dbContext.PurchasePlaces.AnyAsync(
            p => p.UserId == userId && p.Name == purchasePlace.Value.Name, cancellationToken);
        if (duplicate)
        {
            return Result.Failure<Guid>(PurchasePlaceErrors.Duplicate);
        }

        dbContext.PurchasePlaces.Add(purchasePlace.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return purchasePlace.Value.Id;
    }
}
