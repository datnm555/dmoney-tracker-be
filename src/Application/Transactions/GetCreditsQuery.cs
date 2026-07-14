using Application.Abstractions.Messaging;
using Application.Transactions.Data;

namespace Application.Transactions;

/// <summary>Money-in transactions that can reimburse an advance.</summary>
public sealed record GetCreditsQuery : IQuery<List<CreditResponse>>;
