using Application.Abstractions.Data;
using Domain.Accounts;
using Domain.RecurringPayments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.RecurringPayments;

internal static class RecurringPaymentReferences
{
    /// <summary>
    /// Categories and sub-categories are global tables; a sub-category must belong to the category.
    /// The pay-with account must be the owner's.
    /// </summary>
    public static async Task<Result> ValidateAsync(
        IApplicationDbContext dbContext,
        Guid userId,
        Guid? categoryId,
        Guid? subCategoryId,
        Guid? accountId,
        CancellationToken cancellationToken)
    {
        if (categoryId is { } category
            && !await dbContext.Categories.AnyAsync(c => c.Id == category, cancellationToken))
        {
            return Result.Failure(RecurringPaymentErrors.InvalidCategory);
        }

        if (subCategoryId is { } sub
            && !await dbContext.SubCategories.AnyAsync(
                s => s.Id == sub && s.CategoryId == categoryId, cancellationToken))
        {
            return Result.Failure(RecurringPaymentErrors.InvalidSubCategory);
        }

        // Another user's account is "not found", never "forbidden".
        if (accountId is { } account
            && !await dbContext.Accounts.AnyAsync(a => a.Id == account && a.UserId == userId, cancellationToken))
        {
            return Result.Failure(AccountErrors.NotFound);
        }

        return Result.Success();
    }
}
