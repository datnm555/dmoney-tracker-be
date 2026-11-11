using Application.Transactions.Data;

namespace Application.RecurringPayments.Data;

public sealed record RecurringPaymentResponse(
    Guid Id, string Name, Guid? CategoryId, Guid? SubCategoryId, MoneyResponse Amount,
    bool IsVariable, int DayOfMonth, string? PaymentMethod, string? Bank, string? Note, bool IsActive,
    string? StartPeriod,
    string? SavingsBank);
