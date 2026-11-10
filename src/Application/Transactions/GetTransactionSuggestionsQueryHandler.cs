using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Transactions.Data;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Transactions;

internal sealed class GetTransactionSuggestionsQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : IQueryHandler<GetTransactionSuggestionsQuery, List<TransactionSuggestionResponse>>
{
    private const int MinTextLength = 2;
    private const int DefaultLimit = 8;
    private const int MaxLimit = 20;

    /// <summary>Upper bound of matching rows scanned before the in-memory distinct step.</summary>
    private const int CandidateCount = 200;

    public async Task<Result<List<TransactionSuggestionResponse>>> Handle(
        GetTransactionSuggestionsQuery query,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<List<TransactionSuggestionResponse>>(UserErrors.Unauthenticated);
        }

        string text = query.Text?.Trim() ?? string.Empty;
        if (text.Length < MinTextLength)
        {
            return new List<TransactionSuggestionResponse>();
        }

        int limit = query.Limit is null or < 1 ? DefaultLimit : Math.Min(query.Limit.Value, MaxLimit);

        // Case-insensitive "contains": EF/Npgsql translate lower(col) + Contains into a parameterized
        // strpos/LIKE with wildcards escaped, so user text is matched literally (no pattern injection).
        // (EF.Functions.ILike lives in the Npgsql provider, which Application does not reference.)
        string needle = text.ToLowerInvariant();

        // Distinct-by-content is done in memory over a bounded candidate set (latest 200 matches,
        // newest first) so the query stays translatable; the first row seen per content wins.
        // CA1304/CA1311/CA1862 suppressed: EF only translates parameterless ToLower() + Contains(string)
        // (to lower()/strpos on Npgsql); the culture/StringComparison overloads do not translate.
#pragma warning disable CA1304, CA1311, CA1862
        var candidates = await dbContext.Transactions
            .Where(t => t.UserId == userId && t.Content.ToLower().Contains(needle))
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                t.Content,
                CreditAmount = t.Credit.Amount,
                DebitAmount = t.Debit.Amount,
                CreditCurrency = t.Credit.Currency,
                DebitCurrency = t.Debit.Currency,
                t.CategoryId,
                t.SubCategoryId,
                t.PaymentMethod,
                t.CardType,
                t.Bank
            })
            .Take(CandidateCount)
            .ToListAsync(cancellationToken);
#pragma warning restore CA1304, CA1311, CA1862

        return candidates
            .DistinctBy(c => c.Content.Trim(), StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(c => c.CreditAmount > 0
                ? new TransactionSuggestionResponse(
                    c.Content, "in", new MoneyResponse(c.CreditAmount, c.CreditCurrency),
                    c.CategoryId, c.SubCategoryId, c.PaymentMethod, c.CardType, c.Bank)
                : new TransactionSuggestionResponse(
                    c.Content, "out", new MoneyResponse(c.DebitAmount, c.DebitCurrency),
                    c.CategoryId, c.SubCategoryId, c.PaymentMethod, c.CardType, c.Bank))
            .ToList();
    }
}
