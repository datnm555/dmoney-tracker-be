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

internal sealed class CreatePlan : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/plans", async (
            CreatePlanCommand command,
            ICommandHandler<CreatePlanCommand, Guid> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.ToHttpResult(
                localizer,
                id => Results.Created($"/plans/{id}", new { id }));
        }).RequireAuthorization();
    }
}

internal sealed class UpdatePlan : IEndpoint
{
    internal sealed record UpdatePlanRequest(string Name);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/plans/{id:guid}", async (
            Guid id,
            UpdatePlanRequest request,
            ICommandHandler<UpdatePlanCommand> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new UpdatePlanCommand(id, request.Name), cancellationToken);

            return result.ToHttpResult(localizer);
        }).RequireAuthorization();
    }
}

internal sealed class SetDefaultPlan : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/plans/{id:guid}/default", async (
            Guid id,
            ICommandHandler<SetDefaultPlanCommand> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new SetDefaultPlanCommand(id), cancellationToken);

            return result.ToHttpResult(localizer);
        }).RequireAuthorization();
    }
}

internal sealed class DeletePlan : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/plans/{id:guid}", async (
            Guid id,
            ICommandHandler<DeletePlanCommand> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new DeletePlanCommand(id), cancellationToken);

            return result.ToHttpResult(localizer);
        }).RequireAuthorization();
    }
}
