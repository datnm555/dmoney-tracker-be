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
        if (await AlreadyRecordedAsync(command, cancellationToken))
        {
            return Result.Failure<Guid>(RecurringPaymentErrors.AlreadyPaid);
        }

        dbContext.RecurringPaymentRecords.Add(created.Value);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent request may have won the race past the check above; the unique
            // index rejected us. Application cannot see Npgsql, so confirm by re-querying.
            if (await AlreadyRecordedAsync(command, cancellationToken))
            {
                return Result.Failure<Guid>(RecurringPaymentErrors.AlreadyPaid);
            }

            throw;
        }

        return created.Value.Id;
    }

    private Task<bool> AlreadyRecordedAsync(
        CreateRecurringPaymentRecordCommand command,
        CancellationToken cancellationToken) =>
        dbContext.RecurringPaymentRecords
            .AsNoTracking()
            .AnyAsync(
                r => r.RecurringPaymentId == command.RecurringPaymentId && r.Period == command.Period,
                cancellationToken);
}
