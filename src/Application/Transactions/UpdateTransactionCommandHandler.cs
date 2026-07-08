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

        Result updated = transaction.Update(
            command.Date, command.Content, credit.Value, debit.Value,
            command.Note, command.Category,
            command.PaymentMethod, command.CardType, command.Bank, command.IsAdvance);
        if (updated.IsFailure)
        {
            return updated;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
