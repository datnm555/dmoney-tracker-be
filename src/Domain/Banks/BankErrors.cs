using SharedKernel;

namespace Domain.Banks;

public static class BankErrors
{
    public static readonly Error NameRequired = Error.Validation(
        "Banks.NameRequired", "Please enter a bank name.");

    public static readonly Error NameTooLong = Error.Validation(
        "Banks.NameTooLong",
        $"Name must be at most {BankConstants.NameMaxLength} characters.");

    public static readonly Error Duplicate = Error.Conflict(
        "Banks.Duplicate", "This bank already exists.");

    public static readonly Error NotFound = Error.NotFound(
        "Banks.NotFound", "Bank not found.");
}
