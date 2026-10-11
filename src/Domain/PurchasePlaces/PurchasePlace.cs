using SharedKernel;

namespace Domain.PurchasePlaces;

/// <summary>
/// A named place a user buys gold from (e.g. "SJC Trần Nhân Tông", "PNJ").
/// </summary>
public sealed class PurchasePlace : AuditedEntity
{
    private PurchasePlace() { }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public static Result<PurchasePlace> Create(Guid userId, string name)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return Result.Failure<PurchasePlace>(PurchasePlaceErrors.NameRequired);
        }

        if (trimmed.Length > PurchasePlaceConstants.NameMaxLength)
        {
            return Result.Failure<PurchasePlace>(PurchasePlaceErrors.NameTooLong);
        }

        return new PurchasePlace
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = trimmed
        };
    }

    public Result Rename(string name)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return Result.Failure(PurchasePlaceErrors.NameRequired);
        }

        if (trimmed.Length > PurchasePlaceConstants.NameMaxLength)
        {
            return Result.Failure(PurchasePlaceErrors.NameTooLong);
        }

        Name = trimmed;
        return Result.Success();
    }
}
