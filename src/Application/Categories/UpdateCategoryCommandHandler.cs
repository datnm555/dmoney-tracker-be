using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Categories;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Categories;

internal sealed class UpdateCategoryCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<UpdateCategoryCommand>
{
    public async Task<Result> Handle(UpdateCategoryCommand command, CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure(UserErrors.Unauthenticated);
        }

        string? username = await dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => u.Username)
            .FirstOrDefaultAsync(cancellationToken);
        if (username is null)
        {
            return Result.Failure(UserErrors.Unauthenticated);
        }

        Category? category = await dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
        if (category is null)
        {
            return Result.Failure(CategoryErrors.NotFound);
        }

        string trimmedName = command.Name?.Trim() ?? string.Empty;
        bool duplicate = await dbContext.Categories.AnyAsync(
            c => c.Id != command.Id && c.Name == trimmedName, cancellationToken);
        if (duplicate)
        {
            return Result.Failure(CategoryErrors.Duplicate);
        }

        Result updated = category.Update(command.Name!, command.Icon!, username);
        if (updated.IsFailure)
        {
            return updated;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
