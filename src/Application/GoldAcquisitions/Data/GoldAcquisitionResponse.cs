using Application.Transactions.Data;

namespace Application.GoldAcquisitions.Data;

public sealed record GoldAcquisitionResponse(
    Guid Id,
    DateOnly Date,
    Guid GoldTypeId,
    string GoldTypeName,
    decimal Quantity,
    MoneyResponse UnitPrice,
    MoneyResponse Value,
    string? Note);
