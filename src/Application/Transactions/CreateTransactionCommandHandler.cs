using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Transactions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Transactions;

internal sealed class CreateTransactionCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<CreateTransactionCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        CreateTransactionCommand command,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<Guid>(UserErrors.Unauthenticated);
        }

        Result<Money> credit = Money.Create(command.CreditAmount);
        if (credit.IsFailure)
        {
            return Result.Failure<Guid>(credit.Error);
        }

        Result<Money> debit = Money.Create(command.DebitAmount);
        if (debit.IsFailure)
        {
            return Result.Failure<Guid>(debit.Error);
        }

        if (command.AdvanceTransactionId is { } advanceId)
        {
            Result advanceCheck = await ValidateAdvanceLinkAsync(advanceId, userId, null, cancellationToken);
            if (advanceCheck.IsFailure)
            {
                return Result.Failure<Guid>(advanceCheck.Error);
            }
        }

        if (command.PrepaidTransactionId is { } prepaidId)
        {
            bool prepaidExists = await dbContext.Transactions.AnyAsync(
                t => t.Id == prepaidId && t.UserId == userId && t.IsPrepaid, cancellationToken);
            if (!prepaidExists)
            {
                return Result.Failure<Guid>(TransactionErrors.PrepaidNotFound);
            }
        }

        Result<Transaction> transaction = Transaction.Create(
            userId, command.Date, command.Content, credit.Value, debit.Value,
            command.Note, command.Category,
            command.PaymentMethod, command.CardType, command.Bank, command.IsAdvance,
            command.AdvanceTransactionId,
            command.IsPrepaid, command.PrepaidFrom, command.PrepaidTo,
            command.PrepaidTransactionId);
        if (transaction.IsFailure)
        {
            return Result.Failure<Guid>(transaction.Error);
        }

        dbContext.Transactions.Add(transaction.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return transaction.Value.Id;
    }

    private async Task<Result> ValidateAdvanceLinkAsync(
        Guid advanceId, Guid userId, Guid? excludeTransactionId, CancellationToken cancellationToken)
    {
        bool advanceExists = await dbContext.Transactions.AnyAsync(
            t => t.Id == advanceId && t.UserId == userId && t.IsAdvance, cancellationToken);
        if (!advanceExists)
        {
            return Result.Failure(TransactionErrors.AdvanceNotFound);
        }

        bool alreadySettled = await dbContext.Transactions.AnyAsync(
            t => t.AdvanceTransactionId == advanceId && t.Id != excludeTransactionId, cancellationToken);
        return alreadySettled ? Result.Failure(TransactionErrors.AdvanceAlreadySettled) : Result.Success();
    }
}
