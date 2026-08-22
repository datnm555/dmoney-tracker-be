using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Beneficiaries;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Beneficiaries;

internal sealed class SetDefaultBeneficiaryCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<SetDefaultBeneficiaryCommand>
{
    public async Task<Result> Handle(SetDefaultBeneficiaryCommand command, CancellationToken cancellationToken)
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

        if (beneficiary.IsDefault)
        {
            return Result.Success();
        }

        List<Beneficiary> currentDefaults = await dbContext.Beneficiaries
            .Where(b => b.UserId == userId && b.IsDefault)
            .ToListAsync(cancellationToken);
        foreach (Beneficiary current in currentDefaults)
        {
            current.ClearDefault();
        }

        beneficiary.MakeDefault();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
