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
        if (userContext.UserId is not { } userId)
        {
            return Result.Failure<List<SubCategoryResponse>>(UserErrors.Unauthenticated);
        }

        IQueryable<SubCategory> scope = dbContext.SubCategories.Where(s => s.UserId == userId);
        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            scope = scope.Where(s => s.Category == query.Category);
        }

        List<SubCategoryResponse> subCategories = await scope
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Name)
            .Select(s => new SubCategoryResponse(s.Id, s.Category, s.Name, s.IsDefault))
            .ToListAsync(cancellationToken);

        return subCategories;
    }
}
