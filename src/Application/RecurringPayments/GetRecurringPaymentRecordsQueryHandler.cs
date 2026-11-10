using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.RecurringPayments.Data;
using Domain.RecurringPayments;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.RecurringPayments;

internal sealed class GetRecurringPaymentRecordsQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : IQueryHandler<GetRecurringPaymentRecordsQuery, List<RecurringPaymentRecordResponse>>
{
    public async Task<Result<List<RecurringPaymentRecordResponse>>> Handle(
        GetRecurringPaymentRecordsQuery query,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<List<RecurringPaymentRecordResponse>>(UserErrors.Unauthenticated);
        }

        bool owned = await dbContext.RecurringPayments
            .AnyAsync(r => r.Id == query.RecurringPaymentId && r.UserId == userId, cancellationToken);
        if (!owned)
        {
            return Result.Failure<List<RecurringPaymentRecordResponse>>(RecurringPaymentErrors.NotFound);
        }

        List<RecurringPaymentRecord> records = await dbContext.RecurringPaymentRecords
            .Where(r => r.RecurringPaymentId == query.RecurringPaymentId)
            .OrderByDescending(r => r.Period)
            .ToListAsync(cancellationToken);

        return records.Select(r => r.ToResponse()).ToList();
    }
}
