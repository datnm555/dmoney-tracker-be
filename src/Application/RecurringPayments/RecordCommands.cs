using Application.Abstractions.Messaging;

namespace Application.RecurringPayments;

public sealed record CreateRecurringPaymentRecordCommand(
    Guid RecurringPaymentId,
    string Period,
    decimal Amount,
    DateOnly PaidDate,
    Guid? TransactionId = null) : ICommand<Guid>;

public sealed record DeleteRecurringPaymentRecordCommand(Guid Id) : ICommand;
