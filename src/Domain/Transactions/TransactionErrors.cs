using SharedKernel;

namespace Domain.Transactions;

public static class TransactionErrors
{
    public static readonly Error DateRequired = Error.Validation(
        "Transactions.DateRequired",
        "Please pick a date.");

    public static readonly Error CategoryRequired = Error.Validation(
        "Transactions.CategoryRequired",
        "Please choose a category.");

    public static readonly Error NotFound = Error.NotFound(
        "Transactions.NotFound",
        "Record not found.");

    public static readonly Error ContentRequired = Error.Validation(
        "Transactions.ContentRequired",
        "Please enter content.");

    public static readonly Error ContentTooLong = Error.Validation(
        "Transactions.ContentTooLong",
        $"Content must be at most {TransactionConstants.ContentMaxLength} characters.");

    public static readonly Error NoteTooLong = Error.Validation(
        "Transactions.NoteTooLong",
        $"Note must be at most {TransactionConstants.NoteMaxLength} characters.");

    public static readonly Error EmptyAmount = Error.Validation(
        "Transactions.EmptyAmount",
        "At least one amount must be greater than 0.");

    public static readonly Error InvalidCategory = Error.Validation(
        "Transactions.InvalidCategory",
        "Invalid category.");

    public static readonly Error InvalidMonth = Error.Validation(
        "Transactions.InvalidMonth",
        "Invalid month (expected format YYYY or YYYY-MM).");

    public static readonly Error InvalidPaymentMethod = Error.Validation(
        "Transactions.InvalidPaymentMethod",
        "Invalid payment method.");

    public static readonly Error CardTypeRequired = Error.Validation(
        "Transactions.CardTypeRequired",
        "Please select a card type for card payments.");

    public static readonly Error InvalidCardType = Error.Validation(
        "Transactions.InvalidCardType",
        "Invalid card type.");

    public static readonly Error CardDetailsNotAllowed = Error.Validation(
        "Transactions.CardDetailsNotAllowed",
        "Card details are only allowed for card payments.");

    public static readonly Error BankTooLong = Error.Validation(
        "Transactions.BankTooLong",
        $"Bank name must be at most {TransactionConstants.BankMaxLength} characters.");

    public static readonly Error PrepaidOnlyOnCredit = Error.Validation(
        "Transactions.PrepaidOnlyOnCredit",
        "Prepaid only applies to money-in transactions.");

    public static readonly Error PrepaidRangeRequired = Error.Validation(
        "Transactions.PrepaidRangeRequired",
        "Please choose the prepaid period.");

    public static readonly Error PrepaidRangeInvalid = Error.Validation(
        "Transactions.PrepaidRangeInvalid",
        "The prepaid period is invalid.");

    public static readonly Error PrepaidLinkInvalid = Error.Validation(
        "Transactions.PrepaidLinkInvalid",
        "Only a money-out transaction can be linked to a prepaid credit.");

    public static readonly Error PrepaidNotFound = Error.NotFound(
        "Transactions.PrepaidNotFound",
        "Prepaid transaction not found.");

    public static readonly Error ImportEmpty = Error.Validation(
        "Transactions.ImportEmpty",
        "The import contains no rows.");

    public static readonly Error AdvanceLinkInvalid = Error.Validation(
        "Transactions.AdvanceLinkInvalid",
        "Only a non-advance money-in transaction can be linked to an advance.");

    public static readonly Error AdvanceNotFound = Error.NotFound(
        "Transactions.AdvanceNotFound",
        "Advance transaction not found.");

    public static readonly Error AdvanceAlreadySettled = Error.Validation(
        "Transactions.AdvanceAlreadySettled",
        "This advance has already been reimbursed.");

    public static readonly Error ImportTooManyRows = Error.Validation(
        "Transactions.ImportTooManyRows",
        $"An import may contain at most {TransactionConstants.ImportMaxRows} rows.");
}
