using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Categories.Data;
using Domain.Categories;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Categories;

internal sealed class GetCategoriesQueryHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : IQueryHandler<GetCategoriesQuery, List<CategoryResponse>>
{
    public async Task<Result<List<CategoryResponse>>> Handle(
        GetCategoriesQuery query,
        CancellationToken cancellationToken)
    {
        if (userContext.UserId is null)
        {
            return Result.Failure<List<CategoryResponse>>(UserErrors.Unauthenticated);
        }

        List<CategoryResponse> categories = await dbContext.Categories
            .OrderBy(c => c.Code == null)
            .ThenBy(c => c.CreatedAt)
            .Select(c => new CategoryResponse(c.Id, c.Name, c.Icon, c.Code))
            .ToListAsync(cancellationToken);

        return categories;
    }
}
