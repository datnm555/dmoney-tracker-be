using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Categories;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Categories;

internal sealed class DeleteCategoryCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<DeleteCategoryCommand>
{
    public async Task<Result> Handle(
        DeleteCategoryCommand command,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure(UserErrors.Unauthenticated);
        }

        Category? category = await dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == command.Id && c.UserId == userId, cancellationToken);
        if (category is null)
        {
            return Result.Failure(CategoryErrors.NotFound);
        }

        // Transactions and sub-categories reference a custom category by its Id string.
        string code = command.Id.ToString();
        bool inUse = await dbContext.Transactions.AnyAsync(
                         t => t.UserId == userId && t.Category == code, cancellationToken)
                     || await dbContext.SubCategories.AnyAsync(
                         s => s.UserId == userId && s.Category == code, cancellationToken);
        if (inUse)
        {
            return Result.Failure(CategoryErrors.InUse);
        }

        dbContext.Categories.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
