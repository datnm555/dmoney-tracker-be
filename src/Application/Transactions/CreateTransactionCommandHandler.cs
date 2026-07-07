using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Transactions;
using Domain.Users;
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

        Result<Transaction> transaction = Transaction.Create(
            userId, command.Date, command.Content, credit.Value, debit.Value,
            command.Note, command.Category,
            command.PaymentMethod, command.CardType, command.Bank);
        if (transaction.IsFailure)
        {
            return Result.Failure<Guid>(transaction.Error);
        }

        dbContext.Transactions.Add(transaction.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return transaction.Value.Id;
    }
}
