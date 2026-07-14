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
        if (userContext.UserId is null)
        {
            return Result.Failure(UserErrors.Unauthenticated);
        }

        Category? category = await dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
        if (category is null)
        {
            return Result.Failure(CategoryErrors.NotFound);
        }

        bool inUse = await dbContext.Transactions.AnyAsync(
                         t => t.CategoryId == command.Id, cancellationToken)
                     || await dbContext.SubCategories.AnyAsync(
                         s => s.CategoryId == command.Id, cancellationToken);
        if (inUse)
        {
            return Result.Failure(CategoryErrors.InUse);
        }

        dbContext.Categories.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
