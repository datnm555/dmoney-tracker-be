using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Users.Data;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users;

internal sealed class RefreshTokenCommandHandler(
    IApplicationDbContext dbContext,
    ITokenProvider tokenProvider,
    IDateTimeProvider clock)
    : ICommandHandler<RefreshTokenCommand, LoginResponse>
{
    public async Task<Result<LoginResponse>> Handle(
        RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        string token = (command.RefreshToken ?? string.Empty).Trim();
        if (token.Length == 0)
        {
            return Result.Failure<LoginResponse>(UserErrors.InvalidRefreshToken);
        }

        RefreshToken? stored = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == token, cancellationToken);
        if (stored is null || stored.IsExpired(clock.UtcNow))
        {
            return Result.Failure<LoginResponse>(UserErrors.InvalidRefreshToken);
        }

        User? user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == stored.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<LoginResponse>(UserErrors.InvalidRefreshToken);
        }

        // Rotate: the used token is replaced by a fresh one.
        RefreshToken rotated = RefreshToken.Create(
            user.Id, RefreshTokenGenerator.NewToken(), clock.UtcNow);
        dbContext.RefreshTokens.Remove(stored);
        dbContext.RefreshTokens.Add(rotated);
        await dbContext.SaveChangesAsync(cancellationToken);

        string accessToken = tokenProvider.Create(user);

        return new LoginResponse(
            accessToken, user.Id, user.Email, user.Username, user.DisplayName, rotated.Token);
    }
}
