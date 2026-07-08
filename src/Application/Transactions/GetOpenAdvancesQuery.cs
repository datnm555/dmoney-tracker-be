using Application.Abstractions.Messaging;
using Application.Transactions.Data;

namespace Application.Transactions;

/// <param name="ForTransactionId">When editing a linked credit, keeps its own advance in the list.</param>
public sealed record GetOpenAdvancesQuery(Guid? ForTransactionId) : IQuery<List<AdvanceResponse>>;
