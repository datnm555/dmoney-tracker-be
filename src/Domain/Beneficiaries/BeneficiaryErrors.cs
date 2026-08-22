using SharedKernel;

namespace Domain.Beneficiaries;

public static class BeneficiaryErrors
{
    public static readonly Error NameRequired = Error.Validation(
        "Beneficiaries.NameRequired", "Please enter a name for the person.");

    public static readonly Error NameTooLong = Error.Validation(
        "Beneficiaries.NameTooLong",
        $"Name must be at most {BeneficiaryConstants.NameMaxLength} characters.");

    public static readonly Error Duplicate = Error.Conflict(
        "Beneficiaries.Duplicate", "This person already exists.");

    public static readonly Error NotFound = Error.NotFound(
        "Beneficiaries.NotFound", "Person not found.");

    public static readonly Error InUse = Error.Conflict(
        "Beneficiaries.InUse", "This person is used by transactions and cannot be deleted.");
}
