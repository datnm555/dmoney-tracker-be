using Application.Abstractions.Messaging;

namespace Application.Accounts;

/// <summary>
/// Pays (part of) a credit card statement: the card's debt drops by <see cref="Amount"/> and, when
/// <see cref="FromAccountId"/> is set, the paying account's balance moves by the same amount.
/// </summary>
public sealed record PayStatementCommand(
    Guid AccountId,
    DateOnly Date,
    decimal Amount,
    Guid? FromAccountId = null) : ICommand;
