using Application.Transactions.Data;

namespace Application.RecurringPayments.Data;

public sealed record RecurringPaymentRecordResponse(
    Guid Id, Guid RecurringPaymentId, string Period, MoneyResponse Amount,
    DateOnly PaidDate, Guid? TransactionId);
