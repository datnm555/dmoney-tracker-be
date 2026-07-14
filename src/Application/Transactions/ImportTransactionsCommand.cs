using Application.Abstractions.Messaging;

namespace Application.Transactions;

public sealed record ImportTransactionRow(
    DateOnly Date,
    string Content,
    decimal Amount,
    string? Note);

public sealed record ImportTransactionsCommand(
    IReadOnlyList<ImportTransactionRow> Rows) : ICommand<int>;
