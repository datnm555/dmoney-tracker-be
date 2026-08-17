using Application.Abstractions.Messaging;
using Application.Plans;
using Application.Plans.Data;
using Microsoft.Extensions.Localization;
using SharedKernel;
using Web.Api.Infrastructure;
using Web.Api.Middleware;

namespace Web.Api.Endpoints.Plans;

internal sealed class GetPlans : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/plans", async (
            IQueryHandler<GetPlansQuery, List<PlanResponse>> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<List<PlanResponse>> result = await handler.Handle(
                new GetPlansQuery(), cancellationToken);

            return result.ToHttpResult(localizer, Results.Ok);
        }).RequireAuthorization();
    }
}
