using Application.Abstractions.Messaging;
using Application.RecurringPayments.Data;

namespace Application.RecurringPayments;

public sealed record GetRecurringPaymentRecordsQuery(Guid RecurringPaymentId)
    : IQuery<List<RecurringPaymentRecordResponse>>;
