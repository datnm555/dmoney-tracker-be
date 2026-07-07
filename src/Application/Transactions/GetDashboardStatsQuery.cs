using Application.Abstractions.Messaging;
using Application.Transactions.Data;

namespace Application.Transactions;

public sealed record GetDashboardStatsQuery(string Month) : IQuery<DashboardStatsResponse>;
