using SharedKernel;

namespace Domain.PurchasePlaces;

public static class PurchasePlaceErrors
{
    public static readonly Error NameRequired = Error.Validation(
        "PurchasePlaces.NameRequired", "Please enter a name for the purchase place.");

    public static readonly Error NameTooLong = Error.Validation(
        "PurchasePlaces.NameTooLong",
        $"Name must be at most {PurchasePlaceConstants.NameMaxLength} characters.");

    public static readonly Error Duplicate = Error.Conflict(
        "PurchasePlaces.Duplicate", "This purchase place already exists.");

    public static readonly Error NotFound = Error.NotFound(
        "PurchasePlaces.NotFound", "Purchase place not found.");

    public static readonly Error InUse = Error.Conflict(
        "PurchasePlaces.InUse", "This purchase place is used by gold entries and cannot be deleted.");
}
