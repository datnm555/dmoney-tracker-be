using Application.Abstractions.Messaging;

namespace Application.Transactions;

public sealed record ImportTransactionRow(
    DateOnly Date,
    string Content,
    decimal Amount,
    string? Note);

/// <summary>
/// Imports a statement's rows into a plan. With <see cref="AccountId"/> (the account the statement
/// belongs to) every row is paid with that account, so rows after its latest reading move its balance.
/// </summary>
public sealed record ImportTransactionsCommand(
    IReadOnlyList<ImportTransactionRow> Rows, Guid PlanId, Guid? AccountId = null) : ICommand<int>;
