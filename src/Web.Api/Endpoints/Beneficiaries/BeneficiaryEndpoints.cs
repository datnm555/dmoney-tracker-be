using Application.Abstractions.Messaging;
using Application.Beneficiaries;
using Application.Beneficiaries.Data;
using Microsoft.Extensions.Localization;
using SharedKernel;
using Web.Api.Infrastructure;
using Web.Api.Middleware;

namespace Web.Api.Endpoints.Beneficiaries;

internal sealed class GetBeneficiaries : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/beneficiaries", async (
            IQueryHandler<GetBeneficiariesQuery, List<BeneficiaryResponse>> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<List<BeneficiaryResponse>> result = await handler.Handle(
                new GetBeneficiariesQuery(), cancellationToken);

            return result.ToHttpResult(localizer, Results.Ok);
        }).RequireAuthorization();
    }
}

internal sealed class CreateBeneficiary : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/beneficiaries", async (
            CreateBeneficiaryCommand command,
            ICommandHandler<CreateBeneficiaryCommand, Guid> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.ToHttpResult(
                localizer,
                id => Results.Created($"/beneficiaries/{id}", new { id }));
        }).RequireAuthorization();
    }
}

internal sealed class UpdateBeneficiary : IEndpoint
{
    internal sealed record UpdateBeneficiaryRequest(string Name);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/beneficiaries/{id:guid}", async (
            Guid id,
            UpdateBeneficiaryRequest request,
            ICommandHandler<UpdateBeneficiaryCommand> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new UpdateBeneficiaryCommand(id, request.Name), cancellationToken);

            return result.ToHttpResult(localizer);
        }).RequireAuthorization();
    }
}

internal sealed class SetDefaultBeneficiary : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/beneficiaries/{id:guid}/default", async (
            Guid id,
            ICommandHandler<SetDefaultBeneficiaryCommand> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new SetDefaultBeneficiaryCommand(id), cancellationToken);

            return result.ToHttpResult(localizer);
        }).RequireAuthorization();
    }
}

internal sealed class DeleteBeneficiary : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/beneficiaries/{id:guid}", async (
            Guid id,
            ICommandHandler<DeleteBeneficiaryCommand> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new DeleteBeneficiaryCommand(id), cancellationToken);

            return result.ToHttpResult(localizer);
        }).RequireAuthorization();
    }
}
