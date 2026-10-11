using Application.Abstractions.Messaging;
using Application.PurchasePlaces.Data;

namespace Application.PurchasePlaces;

public sealed record GetPurchasePlacesQuery : IQuery<List<PurchasePlaceResponse>>;
