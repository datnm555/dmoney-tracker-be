using Application.Abstractions.Messaging;
using Application.Savings.Data;

namespace Application.Savings;

/// <summary>User-level savings overview across all plans (no planId).</summary>
public sealed record GetSavingsSummaryQuery : IQuery<SavingsSummaryResponse>;
