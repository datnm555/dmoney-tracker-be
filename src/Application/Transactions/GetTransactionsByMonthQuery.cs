using Application.Abstractions.Messaging;
using Application.Transactions.Data;

namespace Application.Transactions;

public sealed record GetTransactionsByMonthQuery(string Month) : IQuery<MonthlySummaryResponse>;
