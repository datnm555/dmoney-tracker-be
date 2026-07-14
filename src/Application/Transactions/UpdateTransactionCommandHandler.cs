using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Categories;
using Domain.SubCategories;
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


        if (command.PrepaidTransactionId is { } prepaidId)
        {
            bool prepaidExists = await dbContext.Transactions.AnyAsync(
                t => t.Id == prepaidId && t.UserId == userId && t.IsPrepaid, cancellationToken);
            if (!prepaidExists)
            {
                return Result.Failure(TransactionErrors.PrepaidNotFound);
            }
        }

        if (command.CategoryId is { } categoryId)
        {
            bool categoryExists = await dbContext.Categories.AnyAsync(
                c => c.Id == categoryId, cancellationToken);
            if (!categoryExists)
            {
                return Result.Failure(CategoryErrors.NotFound);
            }
        }

        if (command.SubCategoryId is { } subCategoryId)
        {
            SubCategory? subCategory = await dbContext.SubCategories
                .FirstOrDefaultAsync(s => s.Id == subCategoryId, cancellationToken);
            if (subCategory is null)
            {
                return Result.Failure(SubCategoryErrors.NotFound);
            }

            if (subCategory.CategoryId != command.CategoryId)
            {
                return Result.Failure(SubCategoryErrors.CategoryMismatch);
            }
        }

        Result updated = transaction.Update(
            command.Date, command.Content, credit.Value, debit.Value,
            command.Note, command.CategoryId,
            command.PaymentMethod, command.CardType, command.Bank, command.IsAdvance,
            command.IsPrepaid, command.PrepaidFrom, command.PrepaidTo,
            command.PrepaidTransactionId, command.SubCategoryId);
        if (updated.IsFailure)
        {
            return updated;
        }

        // Re-link the reimbursed advances to match the requested set.
        List<Guid> requestedIds = (command.AdvanceTransactionIds ?? []).Distinct().ToList();
        List<Transaction> currentlyLinked = await dbContext.Transactions
            .Where(t => t.ReimbursedByTransactionId == command.Id)
            .ToListAsync(cancellationToken);

        foreach (Transaction advance in currentlyLinked.Where(a => !requestedIds.Contains(a.Id)))
        {
            advance.ClearReimbursement();
        }

        if (requestedIds.Count > 0)
        {
            if (transaction.Credit.Amount <= 0m || transaction.IsAdvance)
            {
                return Result.Failure(TransactionErrors.AdvanceLinkInvalid);
            }

            List<Transaction> advances = await dbContext.Transactions
                .Where(t => requestedIds.Contains(t.Id) && t.UserId == userId && t.IsAdvance)
                .ToListAsync(cancellationToken);
            if (advances.Count != requestedIds.Count)
            {
                return Result.Failure(TransactionErrors.AdvanceNotFound);
            }

            if (advances.Any(a => a.ReimbursedByTransactionId is { } by && by != command.Id))
            {
                return Result.Failure(TransactionErrors.AdvanceAlreadySettled);
            }

            foreach (Transaction advance in advances)
            {
                advance.MarkReimbursedBy(transaction.Id);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
