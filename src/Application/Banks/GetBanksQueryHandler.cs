using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Banks.Data;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Banks;

internal sealed class GetBanksQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : IQueryHandler<GetBanksQuery, List<BankResponse>>
{
    public async Task<Result<List<BankResponse>>> Handle(
        GetBanksQuery query,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<List<BankResponse>>(UserErrors.Unauthenticated);
        }

        return await dbContext.Banks
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.IsDefault)
            .ThenBy(b => b.Name)
            .Select(b => new BankResponse(b.Id, b.Name, b.IsDefault))
            .ToListAsync(cancellationToken);
    }
}
