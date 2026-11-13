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

    /// <summary>Preselected in the transaction form's "Trả bằng"; at most one per user, possibly none.</summary>
    public bool IsDefault { get; private set; }

    /// <summary>
    /// Credit cards only: debt from before the owner started the book, whose spending was never recorded.
    /// Payments into the card entered after <see cref="UntrackedDebtSince"/> pay it off first, and
    /// that part counts as spending ("Trả thẻ tín dụng"); the rest is a transfer.
    /// </summary>
    public decimal? UntrackedDebt { get; private set; }

    /// <summary>When <see cref="UntrackedDebt"/> was last set (UTC); payments entered after it reduce it.</summary>
    public DateTime? UntrackedDebtSince { get; private set; }

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

    /// <summary>
    /// Restates the card's untracked debt as of <paramref name="utcNow"/>; an unchanged amount keeps
    /// its date. Zero or null clears it, and so does any non-card type.
    /// </summary>
    public Result SetUntrackedDebt(decimal? amount, DateTime utcNow)
    {
        if (amount is < 0)
        {
            return Result.Failure(AccountErrors.NegativeAmount);
        }

        decimal? debt = Type == AccountTypes.CreditCard && amount is > 0 ? amount : null;
        if (debt != UntrackedDebt)
        {
            UntrackedDebt = debt;
            UntrackedDebtSince = debt is null ? null : utcNow;
        }

        return Result.Success();
    }

    public void MakeDefault() => IsDefault = true;

    public void ClearDefault() => IsDefault = false;

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
