using Application.Abstractions.Messaging;
using Application.Users;
using Application.Users.Data;
using Microsoft.Extensions.Localization;
using SharedKernel;
using Web.Api.Infrastructure;
using Web.Api.Middleware;

namespace Web.Api.Endpoints.Users;

internal sealed class Login : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/users/login", async (
            LoginCommand command,
            ICommandHandler<LoginCommand, LoginResponse> handler,
            IStringLocalizer<SharedResource> localizer,
            CancellationToken cancellationToken) =>
        {
            Result<LoginResponse> result = await handler.Handle(command, cancellationToken);

            return result.ToHttpResult(localizer);
        });
    }
}
