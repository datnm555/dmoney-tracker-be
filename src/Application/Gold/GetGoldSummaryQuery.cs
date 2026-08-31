using Application.Abstractions.Messaging;
using Application.Gold.Data;

namespace Application.Gold;

public sealed record GetGoldSummaryQuery : IQuery<GoldSummaryResponse>;
