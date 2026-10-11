using Application.Abstractions.Messaging;
using Application.Savings;
using Application.Savings.Data;
using Microsoft.Extensions.Localization;
using SharedKernel;
using Web.Api.Infrastructure;
using Web.Api.Middleware;

namespace Web.Api.Endpoints.Savings;

internal sealed class GetSavingsSummary : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/savings/summary", async (
            IQueryHandler<GetSavingsSummaryQuery, SavingsSummaryResponse> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<SavingsSummaryResponse> result = await handler.Handle(
                new GetSavingsSummaryQuery(), cancellationToken);

            return result.ToHttpResult(localizer, Results.Ok);
        }).RequireAuthorization();
    }
}
