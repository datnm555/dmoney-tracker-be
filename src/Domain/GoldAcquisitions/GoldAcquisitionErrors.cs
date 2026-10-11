using SharedKernel;

namespace Domain.GoldAcquisitions;

public static class GoldAcquisitionErrors
{
    public static readonly Error DateRequired = Error.Validation(
        "GoldAcquisitions.DateRequired", "Please pick a date.");

    public static readonly Error QuantityInvalid = Error.Validation(
        "GoldAcquisitions.QuantityInvalid", "Quantity must be greater than zero.");

    public static readonly Error UnitPriceInvalid = Error.Validation(
        "GoldAcquisitions.UnitPriceInvalid", "Unit price cannot be negative.");

    public static readonly Error NoteTooLong = Error.Validation(
        "GoldAcquisitions.NoteTooLong",
        $"Note must be at most {GoldAcquisitionConstants.NoteMaxLength} characters.");

    public static readonly Error NotFound = Error.NotFound(
        "GoldAcquisitions.NotFound", "Pre-owned gold entry not found.");
}
