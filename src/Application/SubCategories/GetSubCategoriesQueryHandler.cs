using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.SubCategories.Data;
using Domain.SubCategories;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.SubCategories;

internal sealed class GetSubCategoriesQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : IQueryHandler<GetSubCategoriesQuery, List<SubCategoryResponse>>
{
    public async Task<Result<List<SubCategoryResponse>>> Handle(
        GetSubCategoriesQuery query,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is null)
        {
            return Result.Failure<List<SubCategoryResponse>>(UserErrors.Unauthenticated);
        }

        IQueryable<SubCategory> scope = dbContext.SubCategories;
        if (query.CategoryId is { } categoryId)
        {
            scope = scope.Where(s => s.CategoryId == categoryId);
        }

        List<SubCategoryResponse> subCategories = await scope
            .OrderBy(s => s.CreatedAt)
            .ThenBy(s => s.Id)
            .Select(s => new SubCategoryResponse(s.Id, s.CategoryId, s.Name, s.IsDefault, s.Icon))
            .ToListAsync(cancellationToken);

        return subCategories;
    }
}
