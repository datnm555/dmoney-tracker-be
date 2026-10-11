using Application.Abstractions.Messaging;
using Application.GoldAcquisitions.Data;

namespace Application.GoldAcquisitions;

public sealed record GetGoldAcquisitionsQuery : IQuery<List<GoldAcquisitionResponse>>;
