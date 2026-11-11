using Application.Abstractions.Messaging;
using Application.Transactions.Data;

namespace Application.Transactions;

/// <summary>
/// Transactions of a plan for a month (<c>YYYY-MM</c>) or a whole year (<c>YYYY</c>).
/// When <paramref name="From"/>/<paramref name="To"/> (<c>YYYY-MM-DD</c>, inclusive) are given they
/// replace the month and select that day range instead.
/// </summary>
public sealed record GetTransactionsByMonthQuery(string Month, Guid PlanId, string? From = null, string? To = null)
    : IQuery<MonthlySummaryResponse>;
