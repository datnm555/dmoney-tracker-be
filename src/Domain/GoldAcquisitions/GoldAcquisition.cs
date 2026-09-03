using SharedKernel;

namespace Domain.GoldAcquisitions;

/// <summary>
/// A single lot of gold a user already owned before tracking it as a transaction
/// (e.g. inherited, gifted, or bought before using the app). <c>Value</c> (quantity
/// × unit price) is intentionally not stored here — it is computed in projections.
/// </summary>
public sealed class GoldAcquisition : AuditedEntity
{
    private GoldAcquisition() { }

    public Guid UserId { get; private set; }

    public Guid GoldTypeId { get; private set; }

    public DateOnly Date { get; private set; }

    public decimal Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public string? Note { get; private set; }

    /// <summary>Where the gold was acquired. Always optional.</summary>
    public Guid? PurchasePlaceId { get; private set; }

    public static Result<GoldAcquisition> Create(
        Guid userId, Guid goldTypeId, DateOnly date, decimal quantity, decimal unitPrice, string? note,
        Guid? purchasePlaceId = null)
    {
        Result validation = Validate(date, quantity, unitPrice, note);
        if (validation.IsFailure)
        {
            return Result.Failure<GoldAcquisition>(validation.Error);
        }

        return new GoldAcquisition
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            GoldTypeId = goldTypeId,
            Date = date,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Note = Normalize(note),
            PurchasePlaceId = purchasePlaceId
        };
    }

    public Result Update(
        Guid goldTypeId, DateOnly date, decimal quantity, decimal unitPrice, string? note,
        Guid? purchasePlaceId = null)
    {
        Result validation = Validate(date, quantity, unitPrice, note);
        if (validation.IsFailure)
        {
            return validation;
        }

        GoldTypeId = goldTypeId;
        Date = date;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Note = Normalize(note);
        PurchasePlaceId = purchasePlaceId;
        return Result.Success();
    }

    private static Result Validate(DateOnly date, decimal quantity, decimal unitPrice, string? note)
    {
        if (date == default)
        {
            return Result.Failure(GoldAcquisitionErrors.DateRequired);
        }

        if (quantity <= 0m)
        {
            return Result.Failure(GoldAcquisitionErrors.QuantityInvalid);
        }

        if (unitPrice < 0m)
        {
            return Result.Failure(GoldAcquisitionErrors.UnitPriceInvalid);
        }

        if ((note?.Trim().Length ?? 0) > GoldAcquisitionConstants.NoteMaxLength)
        {
            return Result.Failure(GoldAcquisitionErrors.NoteTooLong);
        }

        return Result.Success();
    }

    private static string? Normalize(string? value)
    {
        string? trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
