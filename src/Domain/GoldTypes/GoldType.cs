using SharedKernel;

namespace Domain.GoldTypes;

/// <summary>
/// A named kind of gold a user tracks (e.g. "SJC miếng", "Nhẫn trơn 9999").
/// </summary>
public sealed class GoldType : AuditedEntity
{
    private GoldType() { }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public static Result<GoldType> Create(Guid userId, string name)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return Result.Failure<GoldType>(GoldTypeErrors.NameRequired);
        }

        if (trimmed.Length > GoldTypeConstants.NameMaxLength)
        {
            return Result.Failure<GoldType>(GoldTypeErrors.NameTooLong);
        }

        return new GoldType
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
            return Result.Failure(GoldTypeErrors.NameRequired);
        }

        if (trimmed.Length > GoldTypeConstants.NameMaxLength)
        {
            return Result.Failure(GoldTypeErrors.NameTooLong);
        }

        Name = trimmed;
        return Result.Success();
    }
}
