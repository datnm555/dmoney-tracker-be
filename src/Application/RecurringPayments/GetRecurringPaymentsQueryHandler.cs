using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.RecurringPayments.Data;
using Domain.RecurringPayments;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.RecurringPayments;

internal sealed class GetRecurringPaymentsQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : IQueryHandler<GetRecurringPaymentsQuery, List<RecurringPaymentResponse>>
{
    public async Task<Result<List<RecurringPaymentResponse>>> Handle(
        GetRecurringPaymentsQuery query,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<List<RecurringPaymentResponse>>(UserErrors.Unauthenticated);
        }

        List<RecurringPayment> items = await dbContext.RecurringPayments
            .Where(r => r.UserId == userId)
            .ToListAsync(cancellationToken);

        // Name sorts in memory (culture-aware, a handful of rows per user).
        return items
            .OrderByDescending(r => r.IsActive)
            .ThenBy(r => r.DayOfMonth)
            .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(r => r.ToResponse())
            .ToList();
    }
}
