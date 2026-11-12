using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Banks;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Banks;

internal sealed class UpdateBankCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<UpdateBankCommand>
{
    public async Task<Result> Handle(UpdateBankCommand command, CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure(UserErrors.Unauthenticated);
        }

        Bank? bank = await dbContext.Banks
            .FirstOrDefaultAsync(b => b.Id == command.Id && b.UserId == userId, cancellationToken);
        if (bank is null)
        {
            return Result.Failure(BankErrors.NotFound);
        }

        string oldName = bank.Name;
        Result rename = bank.Rename(command.Name);
        if (rename.IsFailure)
        {
            return rename;
        }

        bool isDuplicate = await dbContext.Banks
            .AnyAsync(b => b.UserId == userId && b.Name == bank.Name && b.Id != command.Id, cancellationToken);
        if (isDuplicate)
        {
            return Result.Failure(BankErrors.Duplicate);
        }

        if (!string.Equals(oldName, bank.Name, StringComparison.Ordinal))
        {
            await RenameEverywhereAsync(userId, oldName, bank.Name, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Accounts, transactions, recurring items and savings store the bank as text picked from this
    /// catalog, so a rename follows them; otherwise they would stop grouping and matching with it.
    /// </summary>
    private async Task RenameEverywhereAsync(Guid userId, string oldName, string newName, CancellationToken cancellationToken)
    {
        await dbContext.Accounts
            .Where(a => a.UserId == userId && a.BankName == oldName)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.BankName, newName), cancellationToken);
        await dbContext.Transactions
            .Where(t => t.UserId == userId && t.Bank == oldName)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Bank, newName), cancellationToken);
        await dbContext.Transactions
            .Where(t => t.UserId == userId && t.SavingsBank == oldName)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.SavingsBank, newName), cancellationToken);
        await dbContext.RecurringPayments
            .Where(r => r.UserId == userId && r.Bank == oldName)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Bank, newName), cancellationToken);
        await dbContext.RecurringPayments
            .Where(r => r.UserId == userId && r.SavingsBank == oldName)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.SavingsBank, newName), cancellationToken);
        // A goal is one per savings place: keep an existing goal under the new name as it is.
        bool goalExists = await dbContext.SavingsGoals
            .AnyAsync(g => g.UserId == userId && g.SavingsBank == newName, cancellationToken);
        if (!goalExists)
        {
            await dbContext.SavingsGoals
                .Where(g => g.UserId == userId && g.SavingsBank == oldName)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.SavingsBank, newName), cancellationToken);
        }
    }
}
