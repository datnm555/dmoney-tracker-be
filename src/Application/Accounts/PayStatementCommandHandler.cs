using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Accounts;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Accounts;

internal sealed class PayStatementCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<PayStatementCommand>
{
    public async Task<Result> Handle(PayStatementCommand command, CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure(UserErrors.Unauthenticated);
        }

        if (command.Amount <= 0)
        {
            return Result.Failure(AccountErrors.InvalidPaymentAmount);
        }

        Account? card = await dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == command.AccountId && a.UserId == userId, cancellationToken);
        if (card is null)
        {
            return Result.Failure(AccountErrors.NotFound);
        }

        if (card.Type != AccountTypes.CreditCard)
        {
            return Result.Failure(AccountErrors.NotCreditCard);
        }

        if (command.FromAccountId == card.Id)
        {
            return Result.Failure(AccountErrors.InvalidSourceAccount);
        }

        Account? source = null;
        if (command.FromAccountId is { } fromId)
        {
            source = await dbContext.Accounts
                .FirstOrDefaultAsync(a => a.Id == fromId && a.UserId == userId, cancellationToken);
            if (source is null)
            {
                return Result.Failure(AccountErrors.NotFound);
            }
        }

        AccountSnapshot? cardLatest = await LatestAsync(card.Id, cancellationToken);
        if (cardLatest is null)
        {
            return Result.Failure(AccountErrors.NoBalance);
        }

        // Start from the balances on the new reading's day so transactions recorded since the last
        // readings are kept.
        DateOnly cardReadingDate = ReadingDate(command.Date, cardLatest);
        AccountBalance cardBalance = await AccountBalances.CurrentAsync(
            dbContext, card, cardLatest, cancellationToken, cardReadingDate);

        // A partial payment keeps the statement open with what is left to pay.
        bool fullyPaid = cardLatest.StatementAmount is not { } statement || command.Amount >= statement;
        Result<AccountSnapshot> cardReading = AccountSnapshot.Create(
            card.Id,
            cardReadingDate,
            Math.Max(0, cardBalance.Amount - command.Amount),
            fullyPaid ? cardLatest.StatementAmount : cardLatest.StatementAmount - command.Amount,
            cardLatest.DueDate,
            cardLatest.InstallmentRemaining,
            fullyPaid,
            note: null,
            cardLatest.StatementUntracked);
        if (cardReading.IsFailure)
        {
            return cardReading;
        }

        dbContext.AccountSnapshots.Add(cardReading.Value);

        if (source is not null)
        {
            AccountSnapshot? sourceLatest = await LatestAsync(source.Id, cancellationToken);
            DateOnly sourceReadingDate = sourceLatest is null ? command.Date : ReadingDate(command.Date, sourceLatest);
            decimal before = (await AccountBalances.CurrentAsync(
                dbContext, source, sourceLatest, cancellationToken, sourceReadingDate)).Amount;
            // Paying from a debt account borrows more; paying from a money account spends what it holds.
            decimal after = AccountTypes.IsDebt(source.Type)
                ? before + command.Amount
                : Math.Max(0, before - command.Amount);
            Result<AccountSnapshot> sourceReading = AccountSnapshot.Create(
                source.Id,
                sourceReadingDate,
                after,
                sourceLatest?.StatementAmount,
                sourceLatest?.DueDate,
                sourceLatest?.InstallmentRemaining,
                sourceLatest?.StatementPaid ?? false,
                note: null,
                sourceLatest?.StatementUntracked ?? false);
            if (sourceReading.IsFailure)
            {
                return sourceReading;
            }

            dbContext.AccountSnapshots.Add(sourceReading.Value);
        }

        // One SaveChanges: both readings are written together or not at all.
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private Task<AccountSnapshot?> LatestAsync(Guid accountId, CancellationToken cancellationToken) =>
        dbContext.AccountSnapshots
            .Where(s => s.AccountId == accountId)
            .OrderByDescending(s => s.Date)
            .ThenByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    // Never date the new reading before the current one, or it would not become the account's state.
    private static DateOnly ReadingDate(DateOnly paid, AccountSnapshot latest) =>
        paid < latest.Date ? latest.Date : paid;
}
