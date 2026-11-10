using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.RecurringPayments;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.RecurringPayments;

internal sealed class DeleteRecurringPaymentRecordCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<DeleteRecurringPaymentRecordCommand>
{
    public async Task<Result> Handle(DeleteRecurringPaymentRecordCommand command, CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure(UserErrors.Unauthenticated);
        }

        RecurringPaymentRecord? record = await dbContext.RecurringPaymentRecords
            .FirstOrDefaultAsync(
                r => r.Id == command.Id
                     && dbContext.RecurringPayments.Any(p => p.Id == r.RecurringPaymentId && p.UserId == userId),
                cancellationToken);
        if (record is null)
        {
            return Result.Failure(RecurringPaymentErrors.RecordNotFound);
        }

        // The linked transaction is intentionally left untouched.
        dbContext.RecurringPaymentRecords.Remove(record);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
