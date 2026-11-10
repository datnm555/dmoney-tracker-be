using Application.Abstractions.Messaging;
using Application.Transactions.Data;

namespace Application.Transactions;

/// <param name="Text">Search text (trimmed; fewer than 2 characters yields an empty list).</param>
/// <param name="Limit">Max rows; clamped to 1-20, a value below 1 falls back to the default of 8.</param>
public sealed record GetTransactionSuggestionsQuery(string? Text, int? Limit)
    : IQuery<List<TransactionSuggestionResponse>>;
