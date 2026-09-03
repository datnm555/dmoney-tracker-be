using Application.Abstractions.Messaging;
using Application.GoldAcquisitions;
using Application.GoldAcquisitions.Data;
using Microsoft.Extensions.Localization;
using SharedKernel;
using Web.Api.Infrastructure;
using Web.Api.Middleware;

namespace Web.Api.Endpoints.GoldAcquisitions;

internal sealed class GetGoldAcquisitions : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/gold/acquisitions", async (
            IQueryHandler<GetGoldAcquisitionsQuery, List<GoldAcquisitionResponse>> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<List<GoldAcquisitionResponse>> result = await handler.Handle(
                new GetGoldAcquisitionsQuery(), cancellationToken);

            return result.ToHttpResult(localizer, Results.Ok);
        }).RequireAuthorization();
    }
}

internal sealed class CreateGoldAcquisition : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/gold/acquisitions", async (
            CreateGoldAcquisitionCommand command,
            ICommandHandler<CreateGoldAcquisitionCommand, Guid> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.ToHttpResult(
                localizer,
                id => Results.Created($"/gold/acquisitions/{id}", new { id }));
        }).RequireAuthorization();
    }
}

internal sealed class UpdateGoldAcquisition : IEndpoint
{
    internal sealed record UpdateGoldAcquisitionRequest(
        Guid GoldTypeId, DateOnly Date, decimal Quantity, decimal UnitPrice, string? Note,
        Guid? PurchasePlaceId = null);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/gold/acquisitions/{id:guid}", async (
            Guid id,
            UpdateGoldAcquisitionRequest request,
            ICommandHandler<UpdateGoldAcquisitionCommand> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new UpdateGoldAcquisitionCommand(
                    id, request.GoldTypeId, request.Date, request.Quantity, request.UnitPrice, request.Note,
                    request.PurchasePlaceId),
                cancellationToken);

            return result.ToHttpResult(localizer);
        }).RequireAuthorization();
    }
}

internal sealed class DeleteGoldAcquisition : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/gold/acquisitions/{id:guid}", async (
            Guid id,
            ICommandHandler<DeleteGoldAcquisitionCommand> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new DeleteGoldAcquisitionCommand(id), cancellationToken);

            return result.ToHttpResult(localizer);
        }).RequireAuthorization();
    }
}
