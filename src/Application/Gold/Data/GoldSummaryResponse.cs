using Application.Transactions.Data;

namespace Application.Gold.Data;

public sealed record GoldSummaryResponse(
    IReadOnlyList<GoldTypeSummaryResponse> Types,
    IReadOnlyList<GoldTransactionResponse> Transactions);

public sealed record GoldTypeSummaryResponse(
    Guid GoldTypeId,
    string Name,
    decimal HeldQuantity,
    decimal BoughtQuantity,
    decimal SoldQuantity,
    MoneyResponse TotalSpent,
    MoneyResponse TotalReceived,
    MoneyResponse AverageCostPerChi);

public sealed record GoldTransactionResponse(
    Guid TransactionId,
    DateOnly Date,
    string Content,
    Guid GoldTypeId,
    string GoldTypeName,
    decimal GoldQuantity,
    MoneyResponse Credit,
    MoneyResponse Debit,
    MoneyResponse PricePerChi);
