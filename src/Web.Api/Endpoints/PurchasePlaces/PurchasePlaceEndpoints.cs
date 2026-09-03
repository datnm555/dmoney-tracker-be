using Application.Abstractions.Messaging;
using Application.PurchasePlaces;
using Application.PurchasePlaces.Data;
using Microsoft.Extensions.Localization;
using SharedKernel;
using Web.Api.Infrastructure;
using Web.Api.Middleware;

namespace Web.Api.Endpoints.PurchasePlaces;

internal sealed class GetPurchasePlaces : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/purchase-places", async (
            IQueryHandler<GetPurchasePlacesQuery, List<PurchasePlaceResponse>> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<List<PurchasePlaceResponse>> result = await handler.Handle(
                new GetPurchasePlacesQuery(), cancellationToken);

            return result.ToHttpResult(localizer, Results.Ok);
        }).RequireAuthorization();
    }
}

internal sealed class CreatePurchasePlace : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/purchase-places", async (
            CreatePurchasePlaceCommand command,
            ICommandHandler<CreatePurchasePlaceCommand, Guid> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.ToHttpResult(
                localizer,
                id => Results.Created($"/purchase-places/{id}", new { id }));
        }).RequireAuthorization();
    }
}

internal sealed class UpdatePurchasePlace : IEndpoint
{
    internal sealed record UpdatePurchasePlaceRequest(string Name);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/purchase-places/{id:guid}", async (
            Guid id,
            UpdatePurchasePlaceRequest request,
            ICommandHandler<UpdatePurchasePlaceCommand> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new UpdatePurchasePlaceCommand(id, request.Name), cancellationToken);

            return result.ToHttpResult(localizer);
        }).RequireAuthorization();
    }
}

internal sealed class DeletePurchasePlace : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/purchase-places/{id:guid}", async (
            Guid id,
            ICommandHandler<DeletePurchasePlaceCommand> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new DeletePurchasePlaceCommand(id), cancellationToken);

            return result.ToHttpResult(localizer);
        }).RequireAuthorization();
    }
}
