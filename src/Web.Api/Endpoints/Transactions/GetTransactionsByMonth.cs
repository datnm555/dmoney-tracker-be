using System.Globalization;
using Application.Abstractions.Messaging;
using Application.Transactions;
using Application.Transactions.Data;
using Microsoft.Extensions.Localization;
using SharedKernel;
using Web.Api.Infrastructure;
using Web.Api.Middleware;

namespace Web.Api.Endpoints.Transactions;

internal sealed class GetTransactionsByMonth : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/transactions", async (
            string? month,
            string? from,
            string? to,
            Guid planId,
            IQueryHandler<GetTransactionsByMonthQuery, MonthlySummaryResponse> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            // ?from=&to= (YYYY-MM-DD, inclusive) select a day range and take precedence over ?month=.
            // Without a range the frontend sends ?month=; this fallback covers direct API use.
            string effectiveMonth = month
                ?? DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);

            Result<MonthlySummaryResponse> result = await handler.Handle(
                new GetTransactionsByMonthQuery(effectiveMonth, planId, from, to),
                cancellationToken);

            return result.ToHttpResult(localizer);
        }).RequireAuthorization();
    }
}
