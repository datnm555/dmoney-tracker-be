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

    public static Result<AccountSnapshot> Create(
        Guid accountId,
        DateOnly date,
        decimal balance,
        decimal? statementAmount = null,
        DateOnly? dueDate = null,
        decimal? installmentRemaining = null,
        bool statementPaid = false,
        string? note = null)
    {
        if (balance < 0 || statementAmount is < 0 || installmentRemaining is < 0)
        {
            return Result.Failure<AccountSnapshot>(AccountErrors.NegativeAmount);
        }

        string? trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmedNote is { Length: > AccountConstants.NoteMaxLength })
        {
            return Result.Failure<AccountSnapshot>(AccountErrors.NoteTooLong);
        }

        return new AccountSnapshot
        {
            Id = Guid.CreateVersion7(),
            AccountId = accountId,
            Date = date,
            Balance = balance,
            StatementAmount = statementAmount,
            DueDate = dueDate,
            InstallmentRemaining = installmentRemaining,
            StatementPaid = statementPaid,
            Note = trimmedNote
        };
    }
}
