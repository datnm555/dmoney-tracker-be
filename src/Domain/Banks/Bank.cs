using SharedKernel;

namespace Domain.Banks;

/// <summary>
/// A bank or e-wallet a user pays through (e.g. "Techcombank", "MoMo").
/// Transactions store the bank as free text; this catalog only feeds the UI.
/// </summary>
public sealed class Bank : AuditedEntity
{
    private Bank() { }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public bool IsDefault { get; private set; }

    /// <summary>Display priority, ascending: the UI lists banks (and pay-with groups) in this order.</summary>
    public int SortOrder { get; private set; }

    public static Result<Bank> Create(Guid userId, string name, int sortOrder = 0)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return Result.Failure<Bank>(BankErrors.NameRequired);
        }

        if (trimmed.Length > BankConstants.NameMaxLength)
        {
            return Result.Failure<Bank>(BankErrors.NameTooLong);
        }

        return new Bank
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = trimmed,
            SortOrder = sortOrder
        };
    }

    public void MakeDefault() => IsDefault = true;

    public void ClearDefault() => IsDefault = false;

    public void MoveTo(int sortOrder) => SortOrder = sortOrder;

    public Result Rename(string name)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return Result.Failure(BankErrors.NameRequired);
        }

        if (trimmed.Length > BankConstants.NameMaxLength)
        {
            return Result.Failure(BankErrors.NameTooLong);
        }

        Name = trimmed;
        return Result.Success();
    }
}
