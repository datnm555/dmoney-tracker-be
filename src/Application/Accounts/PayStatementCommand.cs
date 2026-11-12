using Application.Abstractions.Messaging;

namespace Application.Accounts;

/// <summary>
/// Pays (part of) a credit card statement: the card's debt drops by <see cref="Amount"/> and, when
/// <see cref="FromAccountId"/> is set, the paying account's balance moves by the same amount.
/// With <see cref="PaidByTransaction"/> a transaction paying into the card (its ToAccountId) has
/// already moved both balances, so only the statement is settled and no balance moves again.
/// </summary>
public sealed record PayStatementCommand(
    Guid AccountId,
    DateOnly Date,
    decimal Amount,
    Guid? FromAccountId = null,
    bool PaidByTransaction = false) : ICommand;
