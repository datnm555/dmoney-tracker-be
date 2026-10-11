using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.GoldAcquisitions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.GoldAcquisitions;

internal sealed class DeleteGoldAcquisitionCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<DeleteGoldAcquisitionCommand>
{
    public async Task<Result> Handle(DeleteGoldAcquisitionCommand command, CancellationToken cancellationToken)
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

        dbContext.GoldAcquisitions.Remove(acquisition);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
