using Application.Abstractions.Messaging;
using Application.Transactions;
using Application.Transactions.Data;
using Microsoft.Extensions.Localization;
using SharedKernel;
using Web.Api.Infrastructure;
using Web.Api.Middleware;

namespace Web.Api.Endpoints.Transactions;

internal sealed class GetTransactionSuggestions : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/transactions/suggestions", async (
            string? q,
            int? limit,
            IQueryHandler<GetTransactionSuggestionsQuery, List<TransactionSuggestionResponse>> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<List<TransactionSuggestionResponse>> result = await handler.Handle(
                new GetTransactionSuggestionsQuery(q, limit), cancellationToken);

            return result.ToHttpResult(localizer, Results.Ok);
        }).RequireAuthorization();
    }
}
