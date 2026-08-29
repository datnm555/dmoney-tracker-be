using Application.Abstractions.Messaging;
using Application.GoldTypes;
using Application.GoldTypes.Data;
using Microsoft.Extensions.Localization;
using SharedKernel;
using Web.Api.Infrastructure;
using Web.Api.Middleware;

namespace Web.Api.Endpoints.GoldTypes;

internal sealed class GetGoldTypes : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/gold-types", async (
            IQueryHandler<GetGoldTypesQuery, List<GoldTypeResponse>> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<List<GoldTypeResponse>> result = await handler.Handle(
                new GetGoldTypesQuery(), cancellationToken);

            return result.ToHttpResult(localizer, Results.Ok);
        }).RequireAuthorization();
    }
}

internal sealed class CreateGoldType : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/gold-types", async (
            CreateGoldTypeCommand command,
            ICommandHandler<CreateGoldTypeCommand, Guid> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.ToHttpResult(
                localizer,
                id => Results.Created($"/gold-types/{id}", new { id }));
        }).RequireAuthorization();
    }
}
