using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Beneficiaries;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Beneficiaries;

internal sealed class DeleteBeneficiaryCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<DeleteBeneficiaryCommand>
{
    public async Task<Result> Handle(DeleteBeneficiaryCommand command, CancellationToken cancellationToken)
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

        bool inUse = await dbContext.Transactions.AnyAsync(
            t => t.BeneficiaryId == beneficiary.Id, cancellationToken);
        if (inUse)
        {
            return Result.Failure(BeneficiaryErrors.InUse);
        }

        dbContext.Beneficiaries.Remove(beneficiary);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
