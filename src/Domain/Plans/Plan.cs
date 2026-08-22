using SharedKernel;

namespace Domain.Plans;

/// <summary>
/// A separate ledger ("Sổ"): every transaction belongs to exactly one plan and
/// the UI shows one plan at a time. Each user has exactly one default plan.
/// </summary>
public sealed class Plan : AuditedEntity
{
    private Plan() { }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>The user's default plan (initially "Sổ chính"); never deletable, movable via SetDefault.</summary>
    public bool IsDefault { get; private set; }

    public static Result<Plan> Create(Guid userId, string name, bool isDefault = false)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return Result.Failure<Plan>(PlanErrors.NameRequired);
        }

        if (trimmed.Length > PlanConstants.NameMaxLength)
        {
            return Result.Failure<Plan>(PlanErrors.NameTooLong);
        }

        return new Plan
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = trimmed,
            IsDefault = isDefault
        };
    }

    public void MakeDefault() => IsDefault = true;

    public void ClearDefault() => IsDefault = false;

    public Result Rename(string name)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return Result.Failure(PlanErrors.NameRequired);
        }

        if (trimmed.Length > PlanConstants.NameMaxLength)
        {
            return Result.Failure(PlanErrors.NameTooLong);
        }

        Name = trimmed;
        return Result.Success();
    }
}
