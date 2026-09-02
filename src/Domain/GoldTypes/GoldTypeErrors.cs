using SharedKernel;

namespace Domain.GoldTypes;

public static class GoldTypeErrors
{
    public static readonly Error NameRequired = Error.Validation(
        "GoldTypes.NameRequired", "Please enter a name for the gold type.");

    public static readonly Error NameTooLong = Error.Validation(
        "GoldTypes.NameTooLong",
        $"Name must be at most {GoldTypeConstants.NameMaxLength} characters.");

    public static readonly Error Duplicate = Error.Conflict(
        "GoldTypes.Duplicate", "This gold type already exists.");

    public static readonly Error NotFound = Error.NotFound(
        "GoldTypes.NotFound", "Gold type not found.");

    public static readonly Error InUse = Error.Conflict(
        "GoldTypes.InUse", "This gold type is used by transactions and cannot be deleted.");
}
