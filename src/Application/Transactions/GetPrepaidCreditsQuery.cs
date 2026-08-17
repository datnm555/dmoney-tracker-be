using Application.Abstractions.Messaging;
using Application.Transactions.Data;

namespace Application.Transactions;

public sealed record GetPrepaidCreditsQuery(Guid PlanId) : IQuery<List<PrepaidCreditResponse>>;
