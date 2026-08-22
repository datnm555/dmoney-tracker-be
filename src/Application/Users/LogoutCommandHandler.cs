using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users;

internal sealed class LogoutCommandHandler(IApplicationDbContext dbContext)
    : ICommandHandler<LogoutCommand>
{
    public async Task<Result> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        string token = (command.RefreshToken ?? string.Empty).Trim();
        if (token.Length == 0)
        {
            return Result.Success();
        }

        RefreshToken? stored = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == token, cancellationToken);
        if (stored is not null)
        {
            dbContext.RefreshTokens.Remove(stored);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
