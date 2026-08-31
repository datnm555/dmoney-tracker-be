using Application.Abstractions.Messaging;
using Application.Gold;
using Application.Gold.Data;
using Microsoft.Extensions.Localization;
using SharedKernel;
using Web.Api.Infrastructure;
using Web.Api.Middleware;

namespace Web.Api.Endpoints.Gold;

internal sealed class GetGoldSummary : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/gold/summary", async (
            IQueryHandler<GetGoldSummaryQuery, GoldSummaryResponse> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<GoldSummaryResponse> result = await handler.Handle(
                new GetGoldSummaryQuery(), cancellationToken);

            return result.ToHttpResult(localizer, Results.Ok);
        }).RequireAuthorization();
    }
}
