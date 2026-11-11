using SharedKernel;

namespace Domain.Accounts;

/// <summary>
/// A dated reading of one account (money held, or amount owed for debt types).
/// The newest snapshot by Date then CreatedAt is the account's current state.
/// </summary>
public sealed class AccountSnapshot : AuditedEntity
{
    private AccountSnapshot() { }

    public Guid AccountId { get; private set; }

    public DateOnly Date { get; private set; }

    public decimal Balance { get; private set; }

    public decimal? StatementAmount { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public decimal? InstallmentRemaining { get; private set; }

    public bool StatementPaid { get; private set; }

    public string? Note { get; private set; }

    /// <summary>
    /// The statement covers spending that was never recorded as transactions (before swipe-level
    /// tracking began), so paying it should be recorded as an expense.
    /// </summary>
    public bool StatementUntracked { get; private set; }

    public static Result<AccountSnapshot> Create(
        Guid accountId,
        DateOnly date,
        decimal balance,
        decimal? statementAmount = null,
        DateOnly? dueDate = null,
        decimal? installmentRemaining = null,
        bool statementPaid = false,
        string? note = null,
        bool statementUntracked = false)
    {
        var snapshot = new AccountSnapshot { Id = Guid.CreateVersion7(), AccountId = accountId };
        Result applied = snapshot.Apply(date, balance, statementAmount, dueDate, installmentRemaining, statementPaid, note, statementUntracked);
        return applied.IsFailure ? Result.Failure<AccountSnapshot>(applied.Error) : snapshot;
    }

    public Result Update(
        DateOnly date,
        decimal balance,
        decimal? statementAmount = null,
        DateOnly? dueDate = null,
        decimal? installmentRemaining = null,
        bool statementPaid = false,
        string? note = null,
        bool statementUntracked = false)
    {
        // Validate on a scratch copy so a failed update leaves the tracked entity untouched.
        var scratch = new AccountSnapshot();
        Result validated = scratch.Apply(date, balance, statementAmount, dueDate, installmentRemaining, statementPaid, note, statementUntracked);
        if (validated.IsFailure)
        {
            return validated;
        }

        Date = scratch.Date;
        Balance = scratch.Balance;
        StatementAmount = scratch.StatementAmount;
        DueDate = scratch.DueDate;
        InstallmentRemaining = scratch.InstallmentRemaining;
        StatementPaid = scratch.StatementPaid;
        Note = scratch.Note;
        StatementUntracked = scratch.StatementUntracked;
        return Result.Success();
    }

    private Result Apply(
        DateOnly date,
        decimal balance,
        decimal? statementAmount,
        DateOnly? dueDate,
        decimal? installmentRemaining,
        bool statementPaid,
        string? note,
        bool statementUntracked)
    {
        if (balance < 0 || statementAmount is < 0 || installmentRemaining is < 0)
        {
            return Result.Failure(AccountErrors.NegativeAmount);
        }

        string? trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmedNote is { Length: > AccountConstants.NoteMaxLength })
        {
            return Result.Failure(AccountErrors.NoteTooLong);
        }

        Date = date;
        Balance = balance;
        StatementAmount = statementAmount;
        DueDate = dueDate;
        InstallmentRemaining = installmentRemaining;
        StatementPaid = statementPaid;
        Note = trimmedNote;
        StatementUntracked = statementUntracked;
        return Result.Success();
    }
}
