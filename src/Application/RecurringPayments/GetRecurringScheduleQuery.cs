using Application.Abstractions.Messaging;
using Application.RecurringPayments.Data;

namespace Application.RecurringPayments;

public sealed record GetRecurringScheduleQuery(string Month) : IQuery<List<RecurringScheduleItemResponse>>;
