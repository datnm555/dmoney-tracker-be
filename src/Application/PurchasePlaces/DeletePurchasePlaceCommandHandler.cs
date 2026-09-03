using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.PurchasePlaces;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.PurchasePlaces;

internal sealed class DeletePurchasePlaceCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<DeletePurchasePlaceCommand>
{
    public async Task<Result> Handle(DeletePurchasePlaceCommand command, CancellationToken cancellationToken)
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

        dbContext.PurchasePlaces.Remove(purchasePlace);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
