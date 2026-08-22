using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Beneficiaries.Data;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Beneficiaries;

internal sealed class GetBeneficiariesQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : IQueryHandler<GetBeneficiariesQuery, List<BeneficiaryResponse>>
{
    public async Task<Result<List<BeneficiaryResponse>>> Handle(
        GetBeneficiariesQuery query,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<List<BeneficiaryResponse>>(UserErrors.Unauthenticated);
        }

        return await dbContext.Beneficiaries
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.IsDefault)
            .ThenBy(b => b.Name)
            .Select(b => new BeneficiaryResponse(b.Id, b.Name, b.IsDefault))
            .ToListAsync(cancellationToken);
    }
}
