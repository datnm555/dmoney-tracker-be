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

    public string PaymentMethod { get; private set; } = PaymentMethods.Transfer;

    public string? CardType { get; private set; }

    public string? Bank { get; private set; }

    /// <summary>Paid in advance on someone else's behalf; expected to be reimbursed later.</summary>
    public bool IsAdvance { get; private set; }

    /// <summary>For a money-in row: the advance transaction this credit reimburses.</summary>
    public Guid? AdvanceTransactionId { get; private set; }

    /// <summary>Money received upfront covering a period (PrepaidFrom..PrepaidTo).</summary>
    public bool IsPrepaid { get; private set; }

    public DateOnly? PrepaidFrom { get; private set; }

    public DateOnly? PrepaidTo { get; private set; }

    /// <summary>For a money-out row: the prepaid credit that already covers this expense.</summary>
    public Guid? PrepaidTransactionId { get; private set; }

    public static Result<Transaction> Create(
        Guid userId,
        DateOnly date,
        string content,
        Money credit,
        Money debit,
        string? note,
        string? category = null,
        string? paymentMethod = null,
        string? cardType = null,
        string? bank = null,
        bool isAdvance = false,
        Guid? advanceTransactionId = null,
        bool isPrepaid = false,
        DateOnly? prepaidFrom = null,
        DateOnly? prepaidTo = null,
        Guid? prepaidTransactionId = null)
    {
        if (advanceTransactionId is not null && (credit.Amount <= 0m || isAdvance))
        {
            return Result.Failure<Transaction>(TransactionErrors.AdvanceLinkInvalid);
        }

        Result prepaidValidation = ValidatePrepaid(
            credit, isPrepaid, prepaidFrom, prepaidTo, prepaidTransactionId);
        if (prepaidValidation.IsFailure)
        {
            return Result.Failure<Transaction>(prepaidValidation.Error);
        }

        string? normalizedCategory = Normalize(category);
        string normalizedPaymentMethod = Normalize(paymentMethod) ?? PaymentMethods.Transfer;
        string? normalizedCardType = Normalize(cardType);
        string? normalizedBank = Normalize(bank);

        Result validation = Validate(
            content, credit, debit, note, normalizedCategory,
            normalizedPaymentMethod, normalizedCardType, normalizedBank,
            prepaidTransactionId is not null);
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
            Category = normalizedCategory,
            PaymentMethod = normalizedPaymentMethod,
            CardType = normalizedCardType,
            Bank = normalizedBank,
            IsAdvance = isAdvance,
            AdvanceTransactionId = advanceTransactionId,
            IsPrepaid = isPrepaid,
            PrepaidFrom = isPrepaid ? prepaidFrom : null,
            PrepaidTo = isPrepaid ? prepaidTo : null,
            PrepaidTransactionId = prepaidTransactionId
        };

        return transaction;
    }

    public Result Update(
        DateOnly date,
        string content,
        Money credit,
        Money debit,
        string? note,
        string? category = null,
        string? paymentMethod = null,
        string? cardType = null,
        string? bank = null,
        bool isAdvance = false,
        Guid? advanceTransactionId = null,
        bool isPrepaid = false,
        DateOnly? prepaidFrom = null,
        DateOnly? prepaidTo = null,
        Guid? prepaidTransactionId = null)
    {
        if (advanceTransactionId is not null && (credit.Amount <= 0m || isAdvance))
        {
            return Result.Failure(TransactionErrors.AdvanceLinkInvalid);
        }

        Result prepaidValidation = ValidatePrepaid(
            credit, isPrepaid, prepaidFrom, prepaidTo, prepaidTransactionId);
        if (prepaidValidation.IsFailure)
        {
            return prepaidValidation;
        }

        string? normalizedCategory = Normalize(category);
        string normalizedPaymentMethod = Normalize(paymentMethod) ?? PaymentMethods.Transfer;
        string? normalizedCardType = Normalize(cardType);
        string? normalizedBank = Normalize(bank);

        Result validation = Validate(
            content, credit, debit, note, normalizedCategory,
            normalizedPaymentMethod, normalizedCardType, normalizedBank,
            prepaidTransactionId is not null);
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
        PaymentMethod = normalizedPaymentMethod;
        CardType = normalizedCardType;
        Bank = normalizedBank;
        IsAdvance = isAdvance;
        AdvanceTransactionId = advanceTransactionId;
        IsPrepaid = isPrepaid;
        PrepaidFrom = isPrepaid ? prepaidFrom : null;
        PrepaidTo = isPrepaid ? prepaidTo : null;
        PrepaidTransactionId = prepaidTransactionId;

        return Result.Success();
    }

    private static Result Validate(
        string content,
        Money credit,
        Money debit,
        string? note,
        string? normalizedCategory,
        string paymentMethod,
        string? cardType,
        string? bank,
        bool coveredByPrepaid = false)
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

        if (credit.Amount == 0m && debit.Amount == 0m && !coveredByPrepaid)
        {
            return Result.Failure(TransactionErrors.EmptyAmount);
        }

        if (normalizedCategory is not null && !TransactionCategories.IsValid(normalizedCategory))
        {
            return Result.Failure(TransactionErrors.InvalidCategory);
        }

        if (!PaymentMethods.IsValid(paymentMethod))
        {
            return Result.Failure(TransactionErrors.InvalidPaymentMethod);
        }

        if (paymentMethod == PaymentMethods.Card)
        {
            if (cardType is null)
            {
                return Result.Failure(TransactionErrors.CardTypeRequired);
            }

            if (!CardTypes.IsValid(cardType))
            {
                return Result.Failure(TransactionErrors.InvalidCardType);
            }
        }
        else if (cardType is not null || bank is not null)
        {
            return Result.Failure(TransactionErrors.CardDetailsNotAllowed);
        }

        if ((bank?.Length ?? 0) > TransactionConstants.BankMaxLength)
        {
            return Result.Failure(TransactionErrors.BankTooLong);
        }

        return Result.Success();
    }

    private static Result ValidatePrepaid(
        Money credit,
        bool isPrepaid,
        DateOnly? prepaidFrom,
        DateOnly? prepaidTo,
        Guid? prepaidTransactionId)
    {
        if (isPrepaid)
        {
            if (credit.Amount <= 0m)
            {
                return Result.Failure(TransactionErrors.PrepaidOnlyOnCredit);
            }

            if (prepaidFrom is null || prepaidTo is null)
            {
                return Result.Failure(TransactionErrors.PrepaidRangeRequired);
            }

            if (prepaidFrom > prepaidTo)
            {
                return Result.Failure(TransactionErrors.PrepaidRangeInvalid);
            }
        }

        if (prepaidTransactionId is not null && (credit.Amount > 0m || isPrepaid))
        {
            return Result.Failure(TransactionErrors.PrepaidLinkInvalid);
        }

        return Result.Success();
    }

    private static string? Normalize(string? value)
    {
        string? trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
