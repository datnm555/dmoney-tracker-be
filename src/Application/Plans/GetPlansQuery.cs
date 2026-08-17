using Application.Abstractions.Messaging;
using Application.Plans.Data;

namespace Application.Plans;

public sealed record GetPlansQuery : IQuery<List<PlanResponse>>;
