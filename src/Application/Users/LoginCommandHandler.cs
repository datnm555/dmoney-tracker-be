using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Users.Data;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users;

internal sealed class LoginCommandHandler(
    IApplicationDbContext dbContext,
    IPasswordHasher passwordHasher,
    ITokenProvider tokenProvider,
    IDateTimeProvider clock)
    : ICommandHandler<LoginCommand, LoginResponse>
{
    public async Task<Result<LoginResponse>> Handle(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        string identifier = (command.Identifier ?? string.Empty).Trim().ToLowerInvariant();

        User? user = await dbContext.Users
            .FirstOrDefaultAsync(
                u => u.Email == identifier || u.Username == identifier,
                cancellationToken);

        if (user is null || !passwordHasher.Verify(command.Password ?? string.Empty, user.PasswordHash))
        {
            return Result.Failure<LoginResponse>(UserErrors.InvalidCredentials);
        }

        string token = tokenProvider.Create(user);

        RefreshToken refreshToken = RefreshToken.Create(
            user.Id, RefreshTokenGenerator.NewToken(), clock.UtcNow);
        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new LoginResponse(
            token, user.Id, user.Email, user.Username, user.DisplayName, refreshToken.Token);
    }
}
