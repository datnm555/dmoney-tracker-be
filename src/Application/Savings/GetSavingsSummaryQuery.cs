using Application.Abstractions.Messaging;
using Application.Savings.Data;

namespace Application.Savings;

/// <summary>Savings overview of the user's default plan (no planId parameter).</summary>
public sealed record GetSavingsSummaryQuery : IQuery<SavingsSummaryResponse>;
