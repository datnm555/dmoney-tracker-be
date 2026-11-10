using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.RecurringPayments;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.RecurringPayments;

internal sealed class DeleteRecurringPaymentCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<DeleteRecurringPaymentCommand>
{
    public async Task<Result> Handle(DeleteRecurringPaymentCommand command, CancellationToken cancellationToken)
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

        // Records are removed by the database cascade; linked transactions are untouched.
        dbContext.RecurringPayments.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
