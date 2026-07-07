using SharedKernel;

namespace Domain.Transactions;

public sealed class Transaction : AuditedEntity
{
    private Transaction() { }

    public Guid UserId { get; private set; }

    public DateOnly Date { get; private set; }

    public string Content { get; private set; } = string.Empty;

    public Money Credit { get; private set; } = Money.Zero();

    public Money Debit { get; private set; } = Money.Zero();

    public string? Note { get; private set; }

    public string? Category { get; private set; }

    public static Result<Transaction> Create(
        Guid userId,
        DateOnly date,
        string content,
        Money credit,
        Money debit,
        string? note,
        string? category = null)
    {
        string? normalizedCategory = Normalize(category);
        Result validation = Validate(content, credit, debit, note, normalizedCategory);
        if (validation.IsFailure)
        {
            return Result.Failure<Transaction>(validation.Error);
        }

        var transaction = new Transaction
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Date = date,
            Content = content.Trim(),
            Credit = credit,
            Debit = debit,
            Note = Normalize(note),
            Category = normalizedCategory
        };

        return transaction;
    }

    public Result Update(
        DateOnly date,
        string content,
        Money credit,
        Money debit,
        string? note,
        string? category = null)
    {
        string? normalizedCategory = Normalize(category);
        Result validation = Validate(content, credit, debit, note, normalizedCategory);
        if (validation.IsFailure)
        {
            return validation;
        }

        Date = date;
        Content = content.Trim();
        Credit = credit;
        Debit = debit;
        Note = Normalize(note);
        Category = normalizedCategory;

        return Result.Success();
    }

    private static Result Validate(
        string content,
        Money credit,
        Money debit,
        string? note,
        string? normalizedCategory)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Result.Failure(TransactionErrors.ContentRequired);
        }

        if (content.Trim().Length > TransactionConstants.ContentMaxLength)
        {
            return Result.Failure(TransactionErrors.ContentTooLong);
        }

        if ((note?.Trim().Length ?? 0) > TransactionConstants.NoteMaxLength)
        {
            return Result.Failure(TransactionErrors.NoteTooLong);
        }

        if (credit.Amount == 0m && debit.Amount == 0m)
        {
            return Result.Failure(TransactionErrors.EmptyAmount);
        }

        if (normalizedCategory is not null && !TransactionCategories.IsValid(normalizedCategory))
        {
            return Result.Failure(TransactionErrors.InvalidCategory);
        }

        return Result.Success();
    }

    private static string? Normalize(string? value)
    {
        string? trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
