using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.SubCategories;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.SubCategories;

internal sealed class UpdateSubCategoryCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<UpdateSubCategoryCommand>
{
    public async Task<Result> Handle(UpdateSubCategoryCommand command, CancellationToken cancellationToken)
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

        SubCategory? subCategory = await dbContext.SubCategories
            .FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken);
        if (subCategory is null)
        {
            return Result.Failure(SubCategoryErrors.NotFound);
        }

        string trimmedName = command.Name?.Trim() ?? string.Empty;
        bool duplicate = await dbContext.SubCategories.AnyAsync(
            s => s.Id != command.Id
                 && s.CategoryId == subCategory.CategoryId
                 && s.Name == trimmedName,
            cancellationToken);
        if (duplicate)
        {
            return Result.Failure(SubCategoryErrors.Duplicate);
        }

        if (command.IsDefault && !subCategory.IsDefault)
        {
            List<SubCategory> currentDefaults = await dbContext.SubCategories
                .Where(s => s.CategoryId == subCategory.CategoryId && s.IsDefault)
                .ToListAsync(cancellationToken);
            foreach (SubCategory current in currentDefaults)
            {
                current.SetDefault(false);
            }
        }

        Result updated = subCategory.Update(command.Name!, command.IsDefault, command.Icon, username);
        if (updated.IsFailure)
        {
            return updated;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
