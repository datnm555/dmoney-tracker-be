using Application.Abstractions.Messaging;

namespace Application.GoldAcquisitions;

public sealed record CreateGoldAcquisitionCommand(
    Guid GoldTypeId, DateOnly Date, decimal Quantity, decimal UnitPrice, string? Note,
    Guid? PurchasePlaceId = null) : ICommand<Guid>;

public sealed record UpdateGoldAcquisitionCommand(
    Guid Id, Guid GoldTypeId, DateOnly Date, decimal Quantity, decimal UnitPrice, string? Note,
    Guid? PurchasePlaceId = null) : ICommand;

public sealed record DeleteGoldAcquisitionCommand(Guid Id) : ICommand;
