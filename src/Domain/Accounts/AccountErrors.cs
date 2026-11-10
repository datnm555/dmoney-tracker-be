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

    public static readonly Error SnapshotNotFound = Error.NotFound(
        "Accounts.SnapshotNotFound", "Balance entry not found.");

    public static readonly Error NoteTooLong = Error.Validation(
        "Accounts.NoteTooLong",
        $"Note must be at most {AccountConstants.NoteMaxLength} characters.");
}
