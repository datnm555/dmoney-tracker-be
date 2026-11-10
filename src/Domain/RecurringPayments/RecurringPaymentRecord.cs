using System.Globalization;
using SharedKernel;

namespace Domain.RecurringPayments;

/// <summary>One payment of one <see cref="RecurringPayment"/> for one month (period YYYY-MM).</summary>
public sealed class RecurringPaymentRecord : AuditedEntity
{
    private RecurringPaymentRecord() { }

    public Guid RecurringPaymentId { get; private set; }

    /// <summary>Month the payment belongs to, formatted YYYY-MM.</summary>
    public string Period { get; private set; } = string.Empty;

    /// <summary>The amount actually paid.</summary>
    public decimal Amount { get; private set; }

    public DateOnly PaidDate { get; private set; }

    public Guid? TransactionId { get; private set; }

    /// <summary>Parses a YYYY-MM period into the first day of that month.</summary>
    public static bool TryParsePeriod(string? period, out DateOnly monthStart) =>
        DateOnly.TryParseExact(
            period + "-01",
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out monthStart);

    public static Result<RecurringPaymentRecord> Create(
        Guid recurringPaymentId,
        string period,
        decimal amount,
        DateOnly paidDate,
        Guid? transactionId)
    {
        if (!TryParsePeriod(period, out _))
        {
            return Result.Failure<RecurringPaymentRecord>(RecurringPaymentErrors.InvalidPeriod);
        }

        if (amount < 0)
        {
            return Result.Failure<RecurringPaymentRecord>(RecurringPaymentErrors.NegativeAmount);
        }

        return new RecurringPaymentRecord
        {
            Id = Guid.CreateVersion7(),
            RecurringPaymentId = recurringPaymentId,
            Period = period,
            Amount = amount,
            PaidDate = paidDate,
            TransactionId = transactionId
        };
    }
}
