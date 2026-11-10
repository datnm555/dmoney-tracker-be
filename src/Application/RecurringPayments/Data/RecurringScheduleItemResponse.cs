namespace Application.RecurringPayments.Data;

public sealed record RecurringScheduleItemResponse(
    RecurringPaymentResponse Item, DateOnly DueDate, RecurringPaymentRecordResponse? Record);
