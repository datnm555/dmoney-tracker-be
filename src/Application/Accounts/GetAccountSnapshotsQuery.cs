using Application.Abstractions.Messaging;
using Application.Accounts.Data;

namespace Application.Accounts;

public sealed record GetAccountSnapshotsQuery(Guid AccountId) : IQuery<List<AccountSnapshotResponse>>;
