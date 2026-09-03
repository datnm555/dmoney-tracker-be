using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.GoldAcquisitions;
using Domain.GoldTypes;
using Domain.PurchasePlaces;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.GoldAcquisitions;

internal sealed class UpdateGoldAcquisitionCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<UpdateGoldAcquisitionCommand>
{
    public async Task<Result> Handle(UpdateGoldAcquisitionCommand command, CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure(UserErrors.Unauthenticated);
        }

        GoldAcquisition? acquisition = await dbContext.GoldAcquisitions
            .FirstOrDefaultAsync(a => a.Id == command.Id && a.UserId == userId, cancellationToken);
        if (acquisition is null)
        {
            return Result.Failure(GoldAcquisitionErrors.NotFound);
        }

        bool goldTypeExists = await dbContext.GoldTypes.AnyAsync(
            g => g.Id == command.GoldTypeId && g.UserId == userId, cancellationToken);
        if (!goldTypeExists)
        {
            return Result.Failure(GoldTypeErrors.NotFound);
        }

        if (command.PurchasePlaceId is { } commandPurchasePlaceId)
        {
            bool purchasePlaceExists = await dbContext.PurchasePlaces.AnyAsync(
                p => p.Id == commandPurchasePlaceId && p.UserId == userId, cancellationToken);
            if (!purchasePlaceExists)
            {
                return Result.Failure(PurchasePlaceErrors.NotFound);
            }
        }

        Result update = acquisition.Update(
            command.GoldTypeId, command.Date, command.Quantity, command.UnitPrice, command.Note,
            command.PurchasePlaceId);
        if (update.IsFailure)
        {
            return update;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
