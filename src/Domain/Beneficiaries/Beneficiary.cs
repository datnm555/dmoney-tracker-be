using SharedKernel;

namespace Domain.Beneficiaries;

/// <summary>
/// A person a transaction is paid to or received from. Each user has exactly one
/// default beneficiary.
/// </summary>
public sealed class Beneficiary : AuditedEntity
{
    private Beneficiary() { }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>The user's default beneficiary; movable via SetDefault.</summary>
    public bool IsDefault { get; private set; }

    public static Result<Beneficiary> Create(Guid userId, string name, bool isDefault = false)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return Result.Failure<Beneficiary>(BeneficiaryErrors.NameRequired);
        }

        if (trimmed.Length > BeneficiaryConstants.NameMaxLength)
        {
            return Result.Failure<Beneficiary>(BeneficiaryErrors.NameTooLong);
        }

        return new Beneficiary
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
            return Result.Failure(BeneficiaryErrors.NameRequired);
        }

        if (trimmed.Length > BeneficiaryConstants.NameMaxLength)
        {
            return Result.Failure(BeneficiaryErrors.NameTooLong);
        }

        Name = trimmed;
        return Result.Success();
    }
}
