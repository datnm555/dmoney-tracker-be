using System.Globalization;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Transactions.Data;
using Domain.Transactions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Transactions;

internal sealed class GetTransactionsByMonthQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : IQueryHandler<GetTransactionsByMonthQuery, MonthlySummaryResponse>
{
    public async Task<Result<MonthlySummaryResponse>> Handle(
        GetTransactionsByMonthQuery query,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<MonthlySummaryResponse>(UserErrors.Unauthenticated);
        }

        if (!DateOnly.TryParseExact(
                query.Month + "-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateOnly monthStart))
        {
            return Result.Failure<MonthlySummaryResponse>(TransactionErrors.InvalidMonth);
        }

        DateOnly nextMonthStart = monthStart.AddMonths(1);

        IQueryable<Transaction> monthScope = dbContext.Transactions
            .Where(t => t.UserId == userId && t.Date >= monthStart && t.Date < nextMonthStart);

        List<TransactionResponse> items = await monthScope
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .Select(t => new TransactionResponse(
                t.Id,
                t.Date,
                t.Content,
                new MoneyResponse(t.Credit.Amount, t.Credit.Currency),
                new MoneyResponse(t.Debit.Amount, t.Debit.Currency),
                t.Note,
                t.Category))
            .ToListAsync(cancellationToken);

        decimal totalCredit = await monthScope.SumAsync(t => t.Credit.Amount, cancellationToken);
        decimal totalDebit = await monthScope.SumAsync(t => t.Debit.Amount, cancellationToken);

        return new MonthlySummaryResponse(
            items,
            new MoneyResponse(totalCredit, Money.DefaultCurrency),
            new MoneyResponse(totalDebit, Money.DefaultCurrency),
            new MoneyResponse(totalCredit - totalDebit, Money.DefaultCurrency));
    }
}
