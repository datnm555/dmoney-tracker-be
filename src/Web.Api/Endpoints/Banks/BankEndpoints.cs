using Application.Abstractions.Messaging;
using Application.Banks;
using Application.Banks.Data;
using Microsoft.Extensions.Localization;
using SharedKernel;
using Web.Api.Infrastructure;
using Web.Api.Middleware;

namespace Web.Api.Endpoints.Banks;

internal sealed class GetBanks : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/banks", async (
            IQueryHandler<GetBanksQuery, List<BankResponse>> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<List<BankResponse>> result = await handler.Handle(
                new GetBanksQuery(), cancellationToken);

            return result.ToHttpResult(localizer, Results.Ok);
        }).RequireAuthorization();
    }
}

internal sealed class CreateBank : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/banks", async (
            CreateBankCommand command,
            ICommandHandler<CreateBankCommand, Guid> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.ToHttpResult(
                localizer,
                id => Results.Created($"/banks/{id}", new { id }));
        }).RequireAuthorization();
    }
}

internal sealed class UpdateBank : IEndpoint
{
    internal sealed record UpdateBankRequest(string Name);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/banks/{id:guid}", async (
            Guid id,
            UpdateBankRequest request,
            ICommandHandler<UpdateBankCommand> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new UpdateBankCommand(id, request.Name), cancellationToken);

            return result.ToHttpResult(localizer);
        }).RequireAuthorization();
    }
}

internal sealed class DeleteBank : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/banks/{id:guid}", async (
            Guid id,
            ICommandHandler<DeleteBankCommand> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new DeleteBankCommand(id), cancellationToken);

            return result.ToHttpResult(localizer);
        }).RequireAuthorization();
    }
}
