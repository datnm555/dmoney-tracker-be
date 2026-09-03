using Application.Abstractions.Messaging;

namespace Application.Transactions;

public sealed record CreateTransactionCommand(
    DateOnly Date,
    string Content,
    decimal CreditAmount,
    decimal DebitAmount,
    string? Note,
    Guid? CategoryId,
    Guid PlanId,
    string? PaymentMethod = null,
    string? CardType = null,
    string? Bank = null,
    bool IsAdvance = false,
    IReadOnlyList<Guid>? AdvanceTransactionIds = null,
    bool IsPrepaid = false,
    DateOnly? PrepaidFrom = null,
    DateOnly? PrepaidTo = null,
    Guid? PrepaidTransactionId = null,
    Guid? SubCategoryId = null,
    Guid? BeneficiaryId = null,
    Guid? GoldTypeId = null,
    decimal? GoldQuantity = null,
    Guid? PurchasePlaceId = null) : ICommand<Guid>;
