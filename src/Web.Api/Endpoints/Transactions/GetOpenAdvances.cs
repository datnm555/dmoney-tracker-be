using Application.Abstractions.Messaging;
using Application.Transactions;
using Application.Transactions.Data;
using Microsoft.Extensions.Localization;
using SharedKernel;
using Web.Api.Infrastructure;
using Web.Api.Middleware;

namespace Web.Api.Endpoints.Transactions;

internal sealed class GetOpenAdvances : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/transactions/advances/open", async (
            Guid? forTransaction,
            IQueryHandler<GetOpenAdvancesQuery, List<AdvanceResponse>> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<List<AdvanceResponse>> result = await handler.Handle(
                new GetOpenAdvancesQuery(forTransaction), cancellationToken);

            return result.ToHttpResult(localizer, Results.Ok);
        }).RequireAuthorization();
    }
}
