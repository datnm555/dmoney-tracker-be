using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.RecurringPayments;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.RecurringPayments;

internal sealed class UpdateRecurringPaymentCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<UpdateRecurringPaymentCommand>
{
    public async Task<Result> Handle(UpdateRecurringPaymentCommand command, CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure(UserErrors.Unauthenticated);
        }

        RecurringPayment? item = await dbContext.RecurringPayments
            .FirstOrDefaultAsync(r => r.Id == command.Id && r.UserId == userId, cancellationToken);
        if (item is null)
        {
            return Result.Failure(RecurringPaymentErrors.NotFound);
        }

        Result references = await RecurringPaymentReferences.ValidateAsync(
            dbContext, command.CategoryId, command.SubCategoryId, cancellationToken);

        if (references.IsFailure)
        {
            return references;
        }

        Result updated = item.Update(
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
            command.SavingsBank);
        if (updated.IsFailure)
        {
            return updated;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
