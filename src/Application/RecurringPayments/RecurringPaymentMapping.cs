using Application.RecurringPayments.Data;
using Application.Transactions.Data;
using Domain.RecurringPayments;
using Domain.Transactions;

namespace Application.RecurringPayments;

internal static class RecurringPaymentMapping
{
    public static RecurringPaymentResponse ToResponse(this RecurringPayment item) => new(
        item.Id,
        item.Name,
        item.CategoryId,
        item.SubCategoryId,
        new MoneyResponse(item.Amount, Money.DefaultCurrency),
        item.IsVariable,
        item.DayOfMonth,
        item.PaymentMethod,
        item.Bank,
        item.Note,
        item.IsActive,
        item.StartPeriod,
        item.SavingsBank,
        item.AccountId);

    public static RecurringPaymentRecordResponse ToResponse(this RecurringPaymentRecord record) => new(
        record.Id,
        record.RecurringPaymentId,
        record.Period,
        new MoneyResponse(record.Amount, Money.DefaultCurrency),
        record.PaidDate,
        record.TransactionId);
}
