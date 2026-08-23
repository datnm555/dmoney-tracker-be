using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Beneficiaries;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Beneficiaries;

internal sealed class CreateBeneficiaryCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<CreateBeneficiaryCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        CreateBeneficiaryCommand command,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<Guid>(UserErrors.Unauthenticated);
        }

        Result<Beneficiary> beneficiary = Beneficiary.Create(userId, command.Name);
        if (beneficiary.IsFailure)
        {
            return Result.Failure<Guid>(beneficiary.Error);
        }

        bool duplicate = await dbContext.Beneficiaries.AnyAsync(
            b => b.UserId == userId && b.Name == beneficiary.Value.Name, cancellationToken);
        if (duplicate)
        {
            return Result.Failure<Guid>(BeneficiaryErrors.Duplicate);
        }

        dbContext.Beneficiaries.Add(beneficiary.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return beneficiary.Value.Id;
    }
}
