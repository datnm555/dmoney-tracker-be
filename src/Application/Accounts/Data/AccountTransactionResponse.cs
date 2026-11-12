using Application.Transactions.Data;

namespace Application.Accounts.Data;

/// <summary>
/// A transaction as it touches one account. <see cref="Effect"/> is the signed change to the account's
/// balance (debt types: + owes more). <see cref="AfterSnapshot"/> = recorded after the latest reading,
/// so it is part of the current balance; earlier ones are already inside that reading.
/// </summary>
public sealed record AccountTransactionResponse(
    Guid Id,
    DateOnly Date,
    string Content,
    MoneyResponse Credit,
    MoneyResponse Debit,
    Guid PlanId,
    bool Incoming,
    MoneyResponse Effect,
    bool AfterSnapshot);
