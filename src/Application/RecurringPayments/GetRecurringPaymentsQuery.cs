using Application.Abstractions.Messaging;
using Application.RecurringPayments.Data;

namespace Application.RecurringPayments;

public sealed record GetRecurringPaymentsQuery : IQuery<List<RecurringPaymentResponse>>;
