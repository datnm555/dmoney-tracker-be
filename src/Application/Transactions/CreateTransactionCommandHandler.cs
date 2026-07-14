using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.SubCategories;
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


        if (command.PrepaidTransactionId is { } prepaidId)
        {
            bool prepaidExists = await dbContext.Transactions.AnyAsync(
                t => t.Id == prepaidId && t.UserId == userId && t.IsPrepaid, cancellationToken);
            if (!prepaidExists)
            {
                return Result.Failure<Guid>(TransactionErrors.PrepaidNotFound);
            }
        }

        if (command.SubCategoryId is { } subCategoryId)
        {
            Result subCheck = await ValidateSubCategoryAsync(
                subCategoryId, userId, command.Category, cancellationToken);
            if (subCheck.IsFailure)
            {
                return Result.Failure<Guid>(subCheck.Error);
            }
        }

        Result<Transaction> transaction = Transaction.Create(
            userId, command.Date, command.Content, credit.Value, debit.Value,
            command.Note, command.Category,
            command.PaymentMethod, command.CardType, command.Bank, command.IsAdvance,
            command.IsPrepaid, command.PrepaidFrom, command.PrepaidTo,
            command.PrepaidTransactionId, command.SubCategoryId);
        if (transaction.IsFailure)
        {
            return Result.Failure<Guid>(transaction.Error);
        }

        if (command.AdvanceTransactionIds is { Count: > 0 } advanceIds)
        {
            Result<List<Transaction>> advances = await LoadAdvancesForLinkingAsync(
                advanceIds, userId, transaction.Value, null, cancellationToken);
            if (advances.IsFailure)
            {
                return Result.Failure<Guid>(advances.Error);
            }

            foreach (Transaction advance in advances.Value)
            {
                advance.MarkReimbursedBy(transaction.Value.Id);
            }
        }

        dbContext.Transactions.Add(transaction.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return transaction.Value.Id;
    }

    private async Task<Result> ValidateSubCategoryAsync(
        Guid subCategoryId, Guid userId, string? category, CancellationToken cancellationToken)
    {
        SubCategory? subCategory = await dbContext.SubCategories
            .FirstOrDefaultAsync(s => s.Id == subCategoryId && s.UserId == userId, cancellationToken);
        if (subCategory is null)
        {
            return Result.Failure(SubCategoryErrors.NotFound);
        }

        return subCategory.Category == category?.Trim()
            ? Result.Success()
            : Result.Failure(SubCategoryErrors.CategoryMismatch);
    }

    private async Task<Result<List<Transaction>>> LoadAdvancesForLinkingAsync(
        IReadOnlyList<Guid> advanceIds,
        Guid userId,
        Transaction credit,
        Guid? reimbursingTransactionId,
        CancellationToken cancellationToken)
    {
        if (credit.Credit.Amount <= 0m || credit.IsAdvance)
        {
            return Result.Failure<List<Transaction>>(TransactionErrors.AdvanceLinkInvalid);
        }

        List<Guid> distinctIds = advanceIds.Distinct().ToList();
        List<Transaction> advances = await dbContext.Transactions
            .Where(t => distinctIds.Contains(t.Id) && t.UserId == userId && t.IsAdvance)
            .ToListAsync(cancellationToken);
        if (advances.Count != distinctIds.Count)
        {
            return Result.Failure<List<Transaction>>(TransactionErrors.AdvanceNotFound);
        }

        bool anySettledByOther = advances.Any(a =>
            a.ReimbursedByTransactionId is { } by && by != reimbursingTransactionId);
        return anySettledByOther
            ? Result.Failure<List<Transaction>>(TransactionErrors.AdvanceAlreadySettled)
            : advances;
    }
}
