using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Transactions.Data;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Transactions;

internal sealed class GetPrepaidCreditsQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : IQueryHandler<GetPrepaidCreditsQuery, List<PrepaidCreditResponse>>
{
    public async Task<Result<List<PrepaidCreditResponse>>> Handle(
        GetPrepaidCreditsQuery query,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<List<PrepaidCreditResponse>>(UserErrors.Unauthenticated);
        }

        // A prepaid credit can cover many expenses, so linked ones stay listed.
        List<PrepaidCreditResponse> credits = await dbContext.Transactions
            .Where(t => t.UserId == userId && t.IsPrepaid)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .Select(t => new PrepaidCreditResponse(
                t.Id,
                t.Date,
                t.Content,
                new MoneyResponse(t.Credit.Amount, t.Credit.Currency),
                t.PrepaidFrom,
                t.PrepaidTo))
            .ToListAsync(cancellationToken);

        return credits;
    }
}
