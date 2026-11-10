using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.RecurringPayments;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.RecurringPayments;

internal sealed class CreateRecurringPaymentRecordCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<CreateRecurringPaymentRecordCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        CreateRecurringPaymentRecordCommand command,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<Guid>(UserErrors.Unauthenticated);
        }

        bool owned = await dbContext.RecurringPayments
            .AnyAsync(r => r.Id == command.RecurringPaymentId && r.UserId == userId, cancellationToken);
        if (!owned)
        {
            return Result.Failure<Guid>(RecurringPaymentErrors.NotFound);
        }

        Result<RecurringPaymentRecord> created = RecurringPaymentRecord.Create(
            command.RecurringPaymentId, command.Period, command.Amount, command.PaidDate, command.TransactionId);
        if (created.IsFailure)
        {
            return Result.Failure<Guid>(created.Error);
        }

        if (command.TransactionId is { } transactionId
            && !await dbContext.Transactions.AnyAsync(
                t => t.Id == transactionId && t.UserId == userId, cancellationToken))
        {
            return Result.Failure<Guid>(RecurringPaymentErrors.InvalidTransaction);
        }

        // Explicit check for a friendly 409; the unique (item, period) index stays as the backstop.
        if (await dbContext.RecurringPaymentRecords.AnyAsync(
                r => r.RecurringPaymentId == command.RecurringPaymentId && r.Period == command.Period,
                cancellationToken))
        {
            return Result.Failure<Guid>(RecurringPaymentErrors.AlreadyPaid);
        }

        dbContext.RecurringPaymentRecords.Add(created.Value);
        await dbContext.SaveChangesAsync(cancellationToken);
        return created.Value.Id;
    }
}
