using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Transactions;
using Domain.Users;
using SharedKernel;

namespace Application.Transactions;

internal sealed class ImportTransactionsCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<ImportTransactionsCommand, int>
{
    public async Task<Result<int>> Handle(
        ImportTransactionsCommand command,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<int>(UserErrors.Unauthenticated);
        }

        if (command.Rows.Count == 0)
        {
            return Result.Failure<int>(TransactionErrors.ImportEmpty);
        }

        if (command.Rows.Count > TransactionConstants.ImportMaxRows)
        {
            return Result.Failure<int>(TransactionErrors.ImportTooManyRows);
        }

        var transactions = new List<Transaction>(command.Rows.Count);
        foreach (ImportTransactionRow row in command.Rows)
        {
            decimal magnitude = Math.Abs(row.Amount);
            Result<Money> credit = Money.Create(row.Amount >= 0m ? magnitude : 0m);
            if (credit.IsFailure)
            {
                return Result.Failure<int>(credit.Error);
            }

            Result<Money> debit = Money.Create(row.Amount < 0m ? magnitude : 0m);
            if (debit.IsFailure)
            {
                return Result.Failure<int>(debit.Error);
            }

            Result<Transaction> transaction = Transaction.Create(
                userId, row.Date, row.Content, credit.Value, debit.Value, row.Note,
                TransactionCategories.Other);
            if (transaction.IsFailure)
            {
                return Result.Failure<int>(transaction.Error);
            }

            transactions.Add(transaction.Value);
        }

        foreach (Transaction transaction in transactions)
        {
            dbContext.Transactions.Add(transaction);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return transactions.Count;
    }
}
