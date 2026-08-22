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
