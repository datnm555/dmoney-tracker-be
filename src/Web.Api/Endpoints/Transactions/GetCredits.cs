using Application.Abstractions.Messaging;
using Application.Transactions;
using Application.Transactions.Data;
using Microsoft.Extensions.Localization;
using SharedKernel;
using Web.Api.Infrastructure;
using Web.Api.Middleware;

namespace Web.Api.Endpoints.Transactions;

internal sealed class GetCredits : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/transactions/credits", async (
            IQueryHandler<GetCreditsQuery, List<CreditResponse>> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<List<CreditResponse>> result = await handler.Handle(
                new GetCreditsQuery(), cancellationToken);

            return result.ToHttpResult(localizer, Results.Ok);
        }).RequireAuthorization();
    }
}
