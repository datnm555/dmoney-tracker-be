using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Beneficiaries;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Beneficiaries;

internal sealed class UpdateBeneficiaryCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<UpdateBeneficiaryCommand>
{
    public async Task<Result> Handle(UpdateBeneficiaryCommand command, CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure(UserErrors.Unauthenticated);
        }

        Beneficiary? beneficiary = await dbContext.Beneficiaries
            .FirstOrDefaultAsync(b => b.Id == command.Id && b.UserId == userId, cancellationToken);
        if (beneficiary is null)
        {
            return Result.Failure(BeneficiaryErrors.NotFound);
        }

        Result rename = beneficiary.Rename(command.Name);
        if (rename.IsFailure)
        {
            return rename;
        }

        bool isDuplicate = await dbContext.Beneficiaries
            .AnyAsync(b => b.UserId == userId && b.Name == command.Name && b.Id != command.Id, cancellationToken);
        if (isDuplicate)
        {
            return Result.Failure(BeneficiaryErrors.Duplicate);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
