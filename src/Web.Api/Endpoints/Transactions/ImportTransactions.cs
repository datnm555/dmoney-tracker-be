using Application.Abstractions.Messaging;
using Application.Transactions;
using Microsoft.Extensions.Localization;
using SharedKernel;
using Web.Api.Infrastructure;
using Web.Api.Middleware;

namespace Web.Api.Endpoints.Transactions;

internal sealed class ImportTransactions : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/transactions/import", async (
            ImportTransactionsCommand command,
            ICommandHandler<ImportTransactionsCommand, int> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<int> result = await handler.Handle(command, cancellationToken);

            return result.ToHttpResult(
                localizer,
                imported => Results.Ok(new { imported }));
        }).RequireAuthorization();
    }
}
