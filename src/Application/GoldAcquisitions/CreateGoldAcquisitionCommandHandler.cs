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

internal sealed class CreateGoldAcquisitionCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<CreateGoldAcquisitionCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        CreateGoldAcquisitionCommand command,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<Guid>(UserErrors.Unauthenticated);
        }

        bool goldTypeExists = await dbContext.GoldTypes.AnyAsync(
            g => g.Id == command.GoldTypeId && g.UserId == userId, cancellationToken);
        if (!goldTypeExists)
        {
            return Result.Failure<Guid>(GoldTypeErrors.NotFound);
        }

        if (command.PurchasePlaceId is { } commandPurchasePlaceId)
        {
            bool purchasePlaceExists = await dbContext.PurchasePlaces.AnyAsync(
                p => p.Id == commandPurchasePlaceId && p.UserId == userId, cancellationToken);
            if (!purchasePlaceExists)
            {
                return Result.Failure<Guid>(PurchasePlaceErrors.NotFound);
            }
        }

        Result<GoldAcquisition> acquisition = GoldAcquisition.Create(
            userId, command.GoldTypeId, command.Date, command.Quantity, command.UnitPrice, command.Note,
            command.PurchasePlaceId);
        if (acquisition.IsFailure)
        {
            return Result.Failure<Guid>(acquisition.Error);
        }

        dbContext.GoldAcquisitions.Add(acquisition.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return acquisition.Value.Id;
    }
}
