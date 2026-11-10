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

    private const int BatchSize = 200;

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

        // Distinct-by-content is done in memory (keeps the query translatable): matches are paged
        // newest first (Date, CreatedAt, Id) in batches and deduped (trimmed, case-insensitive; the
        // first row seen per content is the latest) until `limit` distinct contents are found or the
        // matches are exhausted, so many repeats of one content never hide older distinct ones.
        // CA1304/CA1311/CA1862 suppressed: EF only translates parameterless ToLower() + Contains(string)
        // (to lower()/strpos on Npgsql); the culture/StringComparison overloads do not translate.
        // Assumes the DB lower() handles Vietnamese (UTF8 database locale).
        var picked = new Dictionary<string, TransactionSuggestionResponse>(StringComparer.OrdinalIgnoreCase);
        for (int skip = 0; picked.Count < limit; skip += BatchSize)
        {
#pragma warning disable CA1304, CA1311, CA1862
            var batch = await dbContext.Transactions
                .Where(t => t.UserId == userId && t.Content.ToLower().Contains(needle))
                .OrderByDescending(t => t.Date)
                .ThenByDescending(t => t.CreatedAt)
                .ThenBy(t => t.Id)
                .Skip(skip)
                .Take(BatchSize)
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
                .ToListAsync(cancellationToken);
#pragma warning restore CA1304, CA1311, CA1862

            foreach (var c in batch)
            {
                string key = c.Content.Trim();
                if (picked.ContainsKey(key))
                {
                    continue;
                }

                picked[key] = c.CreditAmount > 0
                    ? new TransactionSuggestionResponse(
                        c.Content, "in", new MoneyResponse(c.CreditAmount, c.CreditCurrency),
                        c.CategoryId, c.SubCategoryId, c.PaymentMethod, c.CardType, c.Bank)
                    : new TransactionSuggestionResponse(
                        c.Content, "out", new MoneyResponse(c.DebitAmount, c.DebitCurrency),
                        c.CategoryId, c.SubCategoryId, c.PaymentMethod, c.CardType, c.Bank);

                if (picked.Count == limit)
                {
                    break;
                }
            }

            if (batch.Count < BatchSize)
            {
                break;
            }
        }

        return picked.Values.ToList();
    }
}
