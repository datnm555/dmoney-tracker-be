using SharedKernel;

namespace Domain.Accounts;

public static class AccountErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Accounts.NotFound", "Account not found.");

    public static readonly Error NameRequired = Error.Validation(
        "Accounts.NameRequired", "Please enter an account name.");

    public static readonly Error NameTooLong = Error.Validation(
        "Accounts.NameTooLong",
        $"Name must be at most {AccountConstants.NameMaxLength} characters.");

    public static readonly Error InvalidType = Error.Validation(
        "Accounts.InvalidType", "Invalid account type.");

    public static readonly Error InvalidLastDigits = Error.Validation(
        "Accounts.InvalidLastDigits",
        $"Last digits must be exactly {AccountConstants.LastDigitsLength} digits.");

    public static readonly Error InvalidDay = Error.Validation(
        "Accounts.InvalidDay",
        $"Day must be between {AccountConstants.MinDay} and {AccountConstants.MaxDay}.");

    public static readonly Error NegativeAmount = Error.Validation(
        "Accounts.NegativeAmount", "Amount must not be negative.");

    public static readonly Error InvalidInterestRate = Error.Validation(
        "Accounts.InvalidInterestRate", "Interest rate must be between 0 and 100.");

    public static readonly Error BankNameTooLong = Error.Validation(
        "Accounts.BankNameTooLong",
        $"Bank name must be at most {AccountConstants.BankNameMaxLength} characters.");

    public static readonly Error SnapshotNotFound = Error.NotFound(
        "Accounts.SnapshotNotFound", "Balance entry not found.");

    public static readonly Error InvalidPaymentAmount = Error.Validation(
        "Accounts.InvalidPaymentAmount", "Payment amount must be greater than zero.");

    public static readonly Error NotCreditCard = Error.Validation(
        "Accounts.NotCreditCard", "Only a credit card statement can be paid.");

    public static readonly Error NoBalance = Error.Validation(
        "Accounts.NoBalance", "Update the card balance before paying its statement.");

    public static readonly Error InvalidSourceAccount = Error.Validation(
        "Accounts.InvalidSourceAccount", "A card cannot pay its own statement.");

    public static readonly Error NoteTooLong = Error.Validation(
        "Accounts.NoteTooLong",
        $"Note must be at most {AccountConstants.NoteMaxLength} characters.");
}
