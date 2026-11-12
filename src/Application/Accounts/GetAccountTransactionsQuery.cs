using Application.Abstractions.Messaging;
using Application.Accounts.Data;

namespace Application.Accounts;

/// <summary>The newest transactions linked to an account (paid with it or into it), across every plan.</summary>
public sealed record GetAccountTransactionsQuery(Guid AccountId) : IQuery<List<AccountTransactionResponse>>;
