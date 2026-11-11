using Application.Abstractions.Messaging;

namespace Application.RecurringPayments;

public sealed record CreateRecurringPaymentCommand(
    string Name,
    Guid? CategoryId,
    Guid? SubCategoryId,
    decimal Amount,
    bool IsVariable,
    int DayOfMonth,
    string? PaymentMethod = null,
    string? Bank = null,
    string? Note = null,
    bool IsActive = true,
    string? StartPeriod = null) : ICommand<Guid>;

public sealed record UpdateRecurringPaymentCommand(
    Guid Id,
    string Name,
    Guid? CategoryId,
    Guid? SubCategoryId,
    decimal Amount,
    bool IsVariable,
    int DayOfMonth,
    string? PaymentMethod = null,
    string? Bank = null,
    string? Note = null,
    bool IsActive = true,
    string? StartPeriod = null) : ICommand;

public sealed record DeleteRecurringPaymentCommand(Guid Id) : ICommand;
