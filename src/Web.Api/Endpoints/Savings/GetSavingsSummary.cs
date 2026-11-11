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

internal sealed class SetSavingsGoal : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/savings/goals", async (
            SetSavingsGoalCommand command,
            ICommandHandler<SetSavingsGoalCommand, Guid> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.ToHttpResult(localizer, id => Results.Ok(new { id }));
        }).RequireAuthorization();
    }
}

internal sealed class DeleteSavingsGoal : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/savings/goals/{id:guid}", async (
            Guid id,
            ICommandHandler<DeleteSavingsGoalCommand> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new DeleteSavingsGoalCommand(id), cancellationToken);

            return result.ToHttpResult(localizer);
        }).RequireAuthorization();
    }
}
