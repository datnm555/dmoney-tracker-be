using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.RecurringPayments;
using Domain.Users;
using SharedKernel;

namespace Application.RecurringPayments;

internal sealed class CreateRecurringPaymentCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<CreateRecurringPaymentCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateRecurringPaymentCommand command, CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<Guid>(UserErrors.Unauthenticated);
        }

        Result<RecurringPayment> item = RecurringPayment.Create(
            userId,
            command.Name,
            command.CategoryId,
            command.SubCategoryId,
            command.Amount,
            command.IsVariable,
            command.DayOfMonth,
            command.PaymentMethod,
            command.Bank,
            command.Note,
            command.IsActive,
            command.StartPeriod,
            command.SavingsBank,
            command.AccountId);
        if (item.IsFailure)
        {
            return Result.Failure<Guid>(item.Error);
        }

        Result references = await RecurringPaymentReferences.ValidateAsync(
            dbContext, userId, command.CategoryId, command.SubCategoryId, command.AccountId, cancellationToken);
        if (references.IsFailure)
        {
            return Result.Failure<Guid>(references.Error);
        }

        dbContext.RecurringPayments.Add(item.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return item.Value.Id;
    }
}
