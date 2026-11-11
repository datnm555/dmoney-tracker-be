using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Banks;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Banks;

internal sealed class ReorderBanksCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<ReorderBanksCommand>
{
    public async Task<Result> Handle(ReorderBanksCommand command, CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure(UserErrors.Unauthenticated);
        }

        List<Bank> banks = await dbContext.Banks
            .Where(b => b.UserId == userId)
            .ToListAsync(cancellationToken);

        // Exactly the user's banks, each once: a partial or foreign list would leave gaps or leak ids.
        bool sameSet = command.Ids.Count == banks.Count
            && command.Ids.Distinct().Count() == banks.Count
            && banks.All(b => command.Ids.Contains(b.Id));
        if (!sameSet)
        {
            return Result.Failure(BankErrors.InvalidOrder);
        }

        Dictionary<Guid, Bank> byId = banks.ToDictionary(b => b.Id);
        for (int i = 0; i < command.Ids.Count; i++)
        {
            byId[command.Ids[i]].MoveTo(i);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
