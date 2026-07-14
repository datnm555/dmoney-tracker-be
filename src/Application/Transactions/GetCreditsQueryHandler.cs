using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Transactions.Data;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Transactions;

internal sealed class GetCreditsQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : IQueryHandler<GetCreditsQuery, List<CreditResponse>>
{
    private const int MaxResults = 100;

    public async Task<Result<List<CreditResponse>>> Handle(
        GetCreditsQuery query,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<List<CreditResponse>>(UserErrors.Unauthenticated);
        }

        List<CreditResponse> credits = await dbContext.Transactions
            .Where(t => t.UserId == userId && t.Credit.Amount > 0m && !t.IsAdvance)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .Take(MaxResults)
            .Select(t => new CreditResponse(
                t.Id,
                t.Date,
                t.Content,
                new MoneyResponse(t.Credit.Amount, t.Credit.Currency)))
            .ToListAsync(cancellationToken);

        return credits;
    }
}
