using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.SubCategories;
using Domain.Transactions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.SubCategories;

internal sealed class CreateSubCategoryCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<CreateSubCategoryCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        CreateSubCategoryCommand command,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<Guid>(UserErrors.Unauthenticated);
        }

        Result<SubCategory> subCategory = SubCategory.Create(
            userId, command.Category, command.Name, command.IsDefault, command.Icon);
        if (subCategory.IsFailure)
        {
            return Result.Failure<Guid>(subCategory.Error);
        }

        if (TransactionCategories.CustomId(subCategory.Value.Category) is { } customCategoryId)
        {
            bool categoryExists = await dbContext.Categories.AnyAsync(
                c => c.Id == customCategoryId && c.UserId == userId, cancellationToken);
            if (!categoryExists)
            {
                return Result.Failure<Guid>(SubCategoryErrors.InvalidCategory);
            }
        }

        bool duplicate = await dbContext.SubCategories.AnyAsync(
            s => s.UserId == userId
                 && s.Category == subCategory.Value.Category
                 && s.Name == subCategory.Value.Name,
            cancellationToken);
        if (duplicate)
        {
            return Result.Failure<Guid>(SubCategoryErrors.Duplicate);
        }

        if (command.IsDefault)
        {
            List<SubCategory> currentDefaults = await dbContext.SubCategories
                .Where(s => s.UserId == userId
                            && s.Category == subCategory.Value.Category
                            && s.IsDefault)
                .ToListAsync(cancellationToken);
            foreach (SubCategory current in currentDefaults)
            {
                current.SetDefault(false);
            }
        }

        dbContext.SubCategories.Add(subCategory.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return subCategory.Value.Id;
    }
}
