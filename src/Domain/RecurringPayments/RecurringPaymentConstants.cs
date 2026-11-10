using System.Globalization;

namespace Domain.RecurringPayments;

public static class RecurringPaymentConstants
{
    public const int NameMaxLength = 100;
    public const int BankMaxLength = 100;
    public const int NoteMaxLength = 500;
    public const int PaymentMethodMaxLength = 20;
    public const int PeriodLength = 7;
    public const int MinDay = 1;
    public const int MaxDay = 31;

    // Fixed culture so name ordering does not depend on the server's current culture.
    public static readonly StringComparer NameComparer =
        StringComparer.Create(new CultureInfo("vi-VN"), ignoreCase: true);
}
