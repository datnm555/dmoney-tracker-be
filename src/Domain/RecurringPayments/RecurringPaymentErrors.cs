using SharedKernel;

namespace Domain.RecurringPayments;

public static class RecurringPaymentErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "RecurringPayments.NotFound", "Recurring payment not found.");

    public static readonly Error NameRequired = Error.Validation(
        "RecurringPayments.NameRequired", "Please enter a name.");

    public static readonly Error NameTooLong = Error.Validation(
        "RecurringPayments.NameTooLong",
        $"Name must be at most {RecurringPaymentConstants.NameMaxLength} characters.");

    public static readonly Error NegativeAmount = Error.Validation(
        "RecurringPayments.NegativeAmount", "Amount must not be negative.");

    public static readonly Error InvalidDay = Error.Validation(
        "RecurringPayments.InvalidDay",
        $"Day must be between {RecurringPaymentConstants.MinDay} and {RecurringPaymentConstants.MaxDay}.");

    public static readonly Error InvalidCategory = Error.Validation(
        "RecurringPayments.InvalidCategory", "Category does not exist.");

    public static readonly Error InvalidSubCategory = Error.Validation(
        "RecurringPayments.InvalidSubCategory", "Sub-category does not exist or does not belong to the category.");

    public static readonly Error InvalidPaymentMethod = Error.Validation(
        "RecurringPayments.InvalidPaymentMethod", "Invalid payment method.");

    public static readonly Error BankTooLong = Error.Validation(
        "RecurringPayments.BankTooLong",
        $"Bank must be at most {RecurringPaymentConstants.BankMaxLength} characters.");

    public static readonly Error NoteTooLong = Error.Validation(
        "RecurringPayments.NoteTooLong",
        $"Note must be at most {RecurringPaymentConstants.NoteMaxLength} characters.");

    public static readonly Error InvalidPeriod = Error.Validation(
        "RecurringPayments.InvalidPeriod", "Period must be in the format YYYY-MM.");

    public static readonly Error AlreadyPaid = Error.Conflict(
        "RecurringPayments.AlreadyPaid", "This item is already recorded as paid for that month.");

    public static readonly Error RecordNotFound = Error.NotFound(
        "RecurringPayments.RecordNotFound", "Payment record not found.");

    public static readonly Error InvalidTransaction = Error.Validation(
        "RecurringPayments.InvalidTransaction", "Transaction does not exist.");
}
