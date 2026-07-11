using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Transactions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Transactions;

internal sealed class UpdateTransactionCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<UpdateTransactionCommand>
{
    public async Task<Result> Handle(UpdateTransactionCommand command, CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure(UserErrors.Unauthenticated);
        }

        Transaction? transaction = await dbContext.Transactions
            .FirstOrDefaultAsync(t => t.Id == command.Id && t.UserId == userId, cancellationToken);

        if (transaction is null)
        {
            return Result.Failure(TransactionErrors.NotFound);
        }

        Result<Money> credit = Money.Create(command.CreditAmount);
        if (credit.IsFailure)
        {
            return Result.Failure(credit.Error);
        }

        Result<Money> debit = Money.Create(command.DebitAmount);
        if (debit.IsFailure)
        {
            return Result.Failure(debit.Error);
        }

        if (command.AdvanceTransactionId is { } advanceId)
        {
            bool advanceExists = await dbContext.Transactions.AnyAsync(
                t => t.Id == advanceId && t.UserId == userId && t.IsAdvance, cancellationToken);
            if (!advanceExists)
            {
                return Result.Failure(TransactionErrors.AdvanceNotFound);
            }

            bool alreadySettled = await dbContext.Transactions.AnyAsync(
                t => t.AdvanceTransactionId == advanceId && t.Id != command.Id, cancellationToken);
            if (alreadySettled)
            {
                return Result.Failure(TransactionErrors.AdvanceAlreadySettled);
            }
        }

        if (command.PrepaidTransactionId is { } prepaidId)
        {
            bool prepaidExists = await dbContext.Transactions.AnyAsync(
                t => t.Id == prepaidId && t.UserId == userId && t.IsPrepaid, cancellationToken);
            if (!prepaidExists)
            {
                return Result.Failure(TransactionErrors.PrepaidNotFound);
            }
        }

        Result updated = transaction.Update(
            command.Date, command.Content, credit.Value, debit.Value,
            command.Note, command.Category,
            command.PaymentMethod, command.CardType, command.Bank, command.IsAdvance,
            command.AdvanceTransactionId,
            command.IsPrepaid, command.PrepaidFrom, command.PrepaidTo,
            command.PrepaidTransactionId);
        if (updated.IsFailure)
        {
            return updated;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
