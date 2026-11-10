using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.RecurringPayments.Data;
using Domain.RecurringPayments;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.RecurringPayments;

internal sealed class GetRecurringScheduleQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : IQueryHandler<GetRecurringScheduleQuery, List<RecurringScheduleItemResponse>>
{
    public async Task<Result<List<RecurringScheduleItemResponse>>> Handle(
        GetRecurringScheduleQuery query,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<List<RecurringScheduleItemResponse>>(UserErrors.Unauthenticated);
        }

        if (!RecurringPaymentRecord.TryParsePeriod(query.Month, out DateOnly monthStart))
        {
            return Result.Failure<List<RecurringScheduleItemResponse>>(RecurringPaymentErrors.InvalidPeriod);
        }

        List<RecurringPayment> items = await dbContext.RecurringPayments
            .Where(r => r.UserId == userId && r.IsActive)
            .ToListAsync(cancellationToken);

        List<Guid> itemIds = items.Select(r => r.Id).ToList();
        Dictionary<Guid, RecurringPaymentRecord> records = await dbContext.RecurringPaymentRecords
            .Where(r => r.Period == query.Month && itemIds.Contains(r.RecurringPaymentId))
            .ToDictionaryAsync(r => r.RecurringPaymentId, cancellationToken);

        int daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);

        return items
            .Select(item => new RecurringScheduleItemResponse(
                item.ToResponse(),
                new DateOnly(monthStart.Year, monthStart.Month, Math.Min(item.DayOfMonth, daysInMonth)),
                records.TryGetValue(item.Id, out RecurringPaymentRecord? record) ? record.ToResponse() : null))
            .OrderBy(r => r.DueDate)
            .ThenBy(r => r.Item.Name, RecurringPaymentConstants.NameComparer)
            .ToList();
    }
}
