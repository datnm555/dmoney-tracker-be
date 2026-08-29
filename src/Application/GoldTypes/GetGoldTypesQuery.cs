using Application.Abstractions.Messaging;
using Application.GoldTypes.Data;

namespace Application.GoldTypes;

public sealed record GetGoldTypesQuery : IQuery<List<GoldTypeResponse>>;
