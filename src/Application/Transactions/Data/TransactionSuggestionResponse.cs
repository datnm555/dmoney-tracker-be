namespace Application.Transactions.Data;

/// <param name="Type">"in" when the transaction credits money, otherwise "out".</param>
/// <param name="Amount">The non-zero side (credit for "in", debit for "out").</param>
public sealed record TransactionSuggestionResponse(
    string Content,
    string Type,
    MoneyResponse Amount,
    Guid? CategoryId,
    Guid? SubCategoryId,
    string PaymentMethod,
    string? CardType,
    string? Bank);
