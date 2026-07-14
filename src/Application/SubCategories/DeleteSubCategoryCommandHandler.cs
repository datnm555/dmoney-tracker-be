using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.SubCategories;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.SubCategories;

internal sealed class DeleteSubCategoryCommandHandler(
    IApplicationDbContext dbContext,
    IUserContext userContext)
    : ICommandHandler<DeleteSubCategoryCommand>
{
    public async Task<Result> Handle(DeleteSubCategoryCommand command, CancellationToken cancellationToken)
    {
        if (userContext.UserId is null)
        {
            return Result.Failure(UserErrors.Unauthenticated);
        }

        SubCategory? subCategory = await dbContext.SubCategories
            .FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken);

        if (subCategory is null)
        {
            return Result.Failure(SubCategoryErrors.NotFound);
        }

        dbContext.SubCategories.Remove(subCategory);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
