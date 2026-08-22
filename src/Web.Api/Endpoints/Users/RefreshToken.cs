using Application.Abstractions.Messaging;
using Application.Users;
using Application.Users.Data;
using Microsoft.Extensions.Localization;
using SharedKernel;
using Web.Api.Infrastructure;
using Web.Api.Middleware;

namespace Web.Api.Endpoints.Users;

internal sealed class RefreshToken : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/users/refresh", async (
            RefreshTokenCommand command,
            ICommandHandler<RefreshTokenCommand, LoginResponse> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<LoginResponse> result = await handler.Handle(command, cancellationToken);

            return result.ToHttpResult(localizer, Results.Ok);
        });
    }
}

internal sealed class Logout : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/users/logout", async (
            LogoutCommand command,
            ICommandHandler<LogoutCommand> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(command, cancellationToken);

            return result.ToHttpResult(localizer);
        });
    }
}
