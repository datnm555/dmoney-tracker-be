using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Categories;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Categories;

internal sealed class CreateCategoryCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<CreateCategoryCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        CreateCategoryCommand command,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<Guid>(UserErrors.Unauthenticated);
        }

        Result<Category> category = Category.Create(userId, command.Name, command.Icon);
        if (category.IsFailure)
        {
            return Result.Failure<Guid>(category.Error);
        }

        bool duplicate = await dbContext.Categories.AnyAsync(
            c => c.UserId == userId && c.Name == category.Value.Name,
            cancellationToken);
        if (duplicate)
        {
            return Result.Failure<Guid>(CategoryErrors.Duplicate);
        }

        dbContext.Categories.Add(category.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return category.Value.Id;
    }
}
