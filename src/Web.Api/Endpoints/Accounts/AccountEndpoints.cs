using Application.Abstractions.Messaging;
using Application.Accounts;
using Application.Accounts.Data;
using Microsoft.Extensions.Localization;
using SharedKernel;
using Web.Api.Infrastructure;
using Web.Api.Middleware;

namespace Web.Api.Endpoints.Accounts;

internal sealed class GetAccounts : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/accounts", async (
            IQueryHandler<GetAccountsQuery, List<AccountResponse>> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<List<AccountResponse>> result = await handler.Handle(
                new GetAccountsQuery(), cancellationToken);

            return result.ToHttpResult(localizer, Results.Ok);
        }).RequireAuthorization();
    }
}

internal sealed class CreateAccount : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/accounts", async (
            CreateAccountCommand command,
            ICommandHandler<CreateAccountCommand, Guid> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.ToHttpResult(
                localizer,
                id => Results.Created($"/accounts/{id}", new { id }));
        }).RequireAuthorization();
    }
}

internal sealed class UpdateAccount : IEndpoint
{
    internal sealed record UpdateAccountRequest(
        string Name,
        string Type,
        string? LastDigits,
        decimal? CreditLimit,
        int? StatementDay,
        int? DueDay,
        decimal? InterestRatePercent);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/accounts/{id:guid}", async (
            Guid id,
            UpdateAccountRequest request,
            ICommandHandler<UpdateAccountCommand> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new UpdateAccountCommand(
                    id,
                    request.Name,
                    request.Type,
                    request.LastDigits,
                    request.CreditLimit,
                    request.StatementDay,
                    request.DueDay,
                    request.InterestRatePercent),
                cancellationToken);

            return result.ToHttpResult(localizer);
        }).RequireAuthorization();
    }
}

internal sealed class DeleteAccount : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/accounts/{id:guid}", async (
            Guid id,
            ICommandHandler<DeleteAccountCommand> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(new DeleteAccountCommand(id), cancellationToken);

            return result.ToHttpResult(localizer);
        }).RequireAuthorization();
    }
}
