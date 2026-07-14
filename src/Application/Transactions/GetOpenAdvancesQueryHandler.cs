using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Transactions.Data;
using Domain.Transactions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Transactions;

internal sealed class GetOpenAdvancesQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : IQueryHandler<GetOpenAdvancesQuery, List<AdvanceResponse>>
{
    public async Task<Result<List<AdvanceResponse>>> Handle(
        GetOpenAdvancesQuery query,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<List<AdvanceResponse>>(UserErrors.Unauthenticated);
        }

        List<AdvanceResponse> advances = await dbContext.Transactions
            .Where(t => t.UserId == userId && t.IsAdvance)
            .Where(t => t.ReimbursedByTransactionId == null
                        || t.ReimbursedByTransactionId == query.ForTransactionId)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .Select(t => new AdvanceResponse(
                t.Id,
                t.Date,
                t.Content,
                new MoneyResponse(t.Debit.Amount, t.Debit.Currency)))
            .ToListAsync(cancellationToken);

        return advances;
    }
}
