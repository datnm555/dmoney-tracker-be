using Domain.Transactions;
using SharedKernel;

namespace Domain.RecurringPayments;

/// <summary>
/// A monthly payment template (rent, utilities, subscriptions). Each month's
/// payment is a <see cref="RecurringPaymentRecord"/>.
/// </summary>
public sealed class RecurringPayment : AuditedEntity
{
    private RecurringPayment() { }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public Guid? CategoryId { get; private set; }

    public Guid? SubCategoryId { get; private set; }

    public decimal Amount { get; private set; }

    public bool IsVariable { get; private set; }

    public int DayOfMonth { get; private set; }

    public string? PaymentMethod { get; private set; }

    public string? Bank { get; private set; }

    public string? Note { get; private set; }

    public bool IsActive { get; private set; } = true;

    /// <summary>First YYYY-MM month the item appears in the schedule; null = every month.</summary>
    public string? StartPeriod { get; private set; }

    /// <summary>"Nơi gửi" prefilled on the transaction when the item is a savings deposit.</summary>
    public string? SavingsBank { get; private set; }

    public static Result<RecurringPayment> Create(
        Guid userId,
        string name,
        Guid? categoryId,
        Guid? subCategoryId,
        decimal amount,
        bool isVariable,
        int dayOfMonth,
        string? paymentMethod,
        string? bank,
        string? note,
        bool isActive,
        string? startPeriod = null,
        string? savingsBank = null)
    {
        var item = new RecurringPayment { Id = Guid.CreateVersion7(), UserId = userId };
        Result applied = item.Apply(name, categoryId, subCategoryId, amount, isVariable, dayOfMonth,
            paymentMethod, bank, note, isActive, startPeriod, savingsBank);
        return applied.IsFailure ? Result.Failure<RecurringPayment>(applied.Error) : item;
    }

    public Result Update(
        string name,
        Guid? categoryId,
        Guid? subCategoryId,
        decimal amount,
        bool isVariable,
        int dayOfMonth,
        string? paymentMethod,
        string? bank,
        string? note,
        bool isActive,
        string? startPeriod = null,
        string? savingsBank = null)
    {
        // Validate on a scratch copy so a failed update leaves the tracked entity untouched.
        var scratch = new RecurringPayment();
        Result validated = scratch.Apply(name, categoryId, subCategoryId, amount, isVariable, dayOfMonth,
            paymentMethod, bank, note, isActive, startPeriod, savingsBank);
        if (validated.IsFailure)
        {
            return validated;
        }

        Name = scratch.Name;
        CategoryId = scratch.CategoryId;
        SubCategoryId = scratch.SubCategoryId;
        Amount = scratch.Amount;
        IsVariable = scratch.IsVariable;
        DayOfMonth = scratch.DayOfMonth;
        PaymentMethod = scratch.PaymentMethod;
        Bank = scratch.Bank;
        Note = scratch.Note;
        IsActive = scratch.IsActive;
        StartPeriod = scratch.StartPeriod;
        SavingsBank = scratch.SavingsBank;
        return Result.Success();
    }

    private Result Apply(
        string name,
        Guid? categoryId,
        Guid? subCategoryId,
        decimal amount,
        bool isVariable,
        int dayOfMonth,
        string? paymentMethod,
        string? bank,
        string? note,
        bool isActive,
        string? startPeriod = null,
        string? savingsBank = null)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return Result.Failure(RecurringPaymentErrors.NameRequired);
        }

        if (trimmed.Length > RecurringPaymentConstants.NameMaxLength)
        {
            return Result.Failure(RecurringPaymentErrors.NameTooLong);
        }

        if (amount < 0)
        {
            return Result.Failure(RecurringPaymentErrors.NegativeAmount);
        }

        if (dayOfMonth is < RecurringPaymentConstants.MinDay or > RecurringPaymentConstants.MaxDay)
        {
            return Result.Failure(RecurringPaymentErrors.InvalidDay);
        }

        if (subCategoryId is not null && categoryId is null)
        {
            return Result.Failure(RecurringPaymentErrors.InvalidSubCategory);
        }

        string? method = string.IsNullOrWhiteSpace(paymentMethod) ? null : paymentMethod.Trim();
        if (method is not null && !PaymentMethods.IsValid(method))
        {
            return Result.Failure(RecurringPaymentErrors.InvalidPaymentMethod);
        }

        string? trimmedBank = string.IsNullOrWhiteSpace(bank) ? null : bank.Trim();
        if (trimmedBank is { Length: > RecurringPaymentConstants.BankMaxLength })
        {
            return Result.Failure(RecurringPaymentErrors.BankTooLong);
        }

        string? trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmedNote is { Length: > RecurringPaymentConstants.NoteMaxLength })
        {
            return Result.Failure(RecurringPaymentErrors.NoteTooLong);
        }

        string? start = string.IsNullOrWhiteSpace(startPeriod) ? null : startPeriod.Trim();
        if (start is not null && !RecurringPaymentRecord.TryParsePeriod(start, out _))
        {
            return Result.Failure(RecurringPaymentErrors.InvalidPeriod);
        }

        string? trimmedSavingsBank = string.IsNullOrWhiteSpace(savingsBank) ? null : savingsBank.Trim();
        if (trimmedSavingsBank is { Length: > RecurringPaymentConstants.BankMaxLength })
        {
            return Result.Failure(RecurringPaymentErrors.BankTooLong);
        }

        Name = trimmed;
        CategoryId = categoryId;
        SubCategoryId = subCategoryId;
        Amount = amount;
        IsVariable = isVariable;
        DayOfMonth = dayOfMonth;
        PaymentMethod = method;
        Bank = trimmedBank;
        Note = trimmedNote;
        IsActive = isActive;
        StartPeriod = start;
        SavingsBank = trimmedSavingsBank;
        return Result.Success();
    }
}
