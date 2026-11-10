using Application.Abstractions.Data;
using Domain.RecurringPayments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.RecurringPayments;

internal static class RecurringPaymentReferences
{
    /// <summary>Categories and sub-categories are global tables; a sub-category must belong to the category.</summary>
    public static async Task<Result> ValidateAsync(
        IApplicationDbContext dbContext,
        Guid? categoryId,
        Guid? subCategoryId,
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

        return Result.Success();
    }
}
