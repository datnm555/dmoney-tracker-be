using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Accounts.Data;
using Domain.Accounts;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Accounts;

internal sealed class GetAccountSnapshotsQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : IQueryHandler<GetAccountSnapshotsQuery, List<AccountSnapshotResponse>>
{
    public async Task<Result<List<AccountSnapshotResponse>>> Handle(
        GetAccountSnapshotsQuery query,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<List<AccountSnapshotResponse>>(UserErrors.Unauthenticated);
        }

        bool owned = await dbContext.Accounts
            .AnyAsync(a => a.Id == query.AccountId && a.UserId == userId, cancellationToken);
        if (!owned)
        {
            return Result.Failure<List<AccountSnapshotResponse>>(AccountErrors.NotFound);
        }

        List<AccountSnapshot> snapshots = await dbContext.AccountSnapshots
            .Where(s => s.AccountId == query.AccountId)
            .OrderByDescending(s => s.Date)
            .ThenByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        return snapshots.Select(s => s.ToResponse()).ToList();
    }
}
