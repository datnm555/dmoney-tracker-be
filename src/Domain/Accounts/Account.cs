using SharedKernel;

namespace Domain.Accounts;

/// <summary>
/// A bank account, e-wallet, cash pocket, overdraft or credit card of a user.
/// Balances live in <see cref="AccountSnapshot"/> rows entered by hand.
/// </summary>
public sealed class Account : AuditedEntity
{
    private Account() { }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Type { get; private set; } = string.Empty;

    public string? LastDigits { get; private set; }

    public decimal? CreditLimit { get; private set; }

    public int? StatementDay { get; private set; }

    public int? DueDay { get; private set; }

    public decimal? InterestRatePercent { get; private set; }

    /// <summary>Free-text bank name used to group accounts; no FK to the banks catalog.</summary>
    public string? BankName { get; private set; }

    public static Result<Account> Create(
        Guid userId,
        string name,
        string type,
        string? lastDigits = null,
        decimal? creditLimit = null,
        int? statementDay = null,
        int? dueDay = null,
        decimal? interestRatePercent = null,
        string? bankName = null)
    {
        var account = new Account { Id = Guid.CreateVersion7(), UserId = userId };
        Result applied = account.Apply(name, type, lastDigits, creditLimit, statementDay, dueDay, interestRatePercent, bankName);
        return applied.IsFailure ? Result.Failure<Account>(applied.Error) : account;
    }

    public Result Update(
        string name,
        string type,
        string? lastDigits = null,
        decimal? creditLimit = null,
        int? statementDay = null,
        int? dueDay = null,
        decimal? interestRatePercent = null,
        string? bankName = null)
    {
        // Validate on a scratch copy so a failed update leaves the tracked entity untouched.
        var scratch = new Account();
        Result validated = scratch.Apply(name, type, lastDigits, creditLimit, statementDay, dueDay, interestRatePercent, bankName);
        if (validated.IsFailure)
        {
            return validated;
        }

        Name = scratch.Name;
        Type = scratch.Type;
        LastDigits = scratch.LastDigits;
        CreditLimit = scratch.CreditLimit;
        StatementDay = scratch.StatementDay;
        DueDay = scratch.DueDay;
        InterestRatePercent = scratch.InterestRatePercent;
        BankName = scratch.BankName;
        return Result.Success();
    }

    private Result Apply(
        string name,
        string type,
        string? lastDigits,
        decimal? creditLimit,
        int? statementDay,
        int? dueDay,
        decimal? interestRatePercent,
        string? bankName)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return Result.Failure(AccountErrors.NameRequired);
        }

        if (trimmed.Length > AccountConstants.NameMaxLength)
        {
            return Result.Failure(AccountErrors.NameTooLong);
        }

        if (type is null || !AccountTypes.IsValid(type))
        {
            return Result.Failure(AccountErrors.InvalidType);
        }

        string? digits = string.IsNullOrWhiteSpace(lastDigits) ? null : lastDigits.Trim();
        if (digits is not null
            && (digits.Length != AccountConstants.LastDigitsLength || !digits.All(char.IsAsciiDigit)))
        {
            return Result.Failure(AccountErrors.InvalidLastDigits);
        }

        if (!IsValidDay(statementDay) || !IsValidDay(dueDay))
        {
            return Result.Failure(AccountErrors.InvalidDay);
        }

        if (creditLimit is < 0)
        {
            return Result.Failure(AccountErrors.NegativeAmount);
        }

        if (interestRatePercent is < 0 or > AccountConstants.MaxInterestRatePercent)
        {
            return Result.Failure(AccountErrors.InvalidInterestRate);
        }

        string? bank = string.IsNullOrWhiteSpace(bankName) ? null : bankName.Trim();
        if (bank is { Length: > AccountConstants.BankNameMaxLength })
        {
            return Result.Failure(AccountErrors.BankNameTooLong);
        }

        Name = trimmed;
        Type = type;
        BankName = bank;
        LastDigits = digits;
        CreditLimit = creditLimit;
        StatementDay = statementDay;
        DueDay = dueDay;
        InterestRatePercent = interestRatePercent;
        return Result.Success();
    }

    private static bool IsValidDay(int? day) =>
        day is null || (day >= AccountConstants.MinDay && day <= AccountConstants.MaxDay);
}
