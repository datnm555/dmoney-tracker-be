using Application.Abstractions.Messaging;
using Application.Accounts.Data;

namespace Application.Accounts;

public sealed record GetAccountsQuery : IQuery<List<AccountResponse>>;
