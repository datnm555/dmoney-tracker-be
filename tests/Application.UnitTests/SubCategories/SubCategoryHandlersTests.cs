using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.SubCategories;
using Domain.SubCategories;
using MockQueryable.NSubstitute;
using NSubstitute;
using Shouldly;

namespace Application.UnitTests.SubCategories;

public class SubCategoryHandlersTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private IApplicationDbContext _dbContext = null!;

    private (CreateSubCategoryCommandHandler create, GetSubCategoriesQueryHandler get) CreateHandlers(
        params SubCategory[] existing)
    {
        _dbContext = Substitute.For<IApplicationDbContext>();
        var userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        var subCategories = existing.ToList().BuildMockDbSet();
        _dbContext.SubCategories.Returns(subCategories);
        return (
            new CreateSubCategoryCommandHandler(_dbContext, userContext),
            new GetSubCategoriesQueryHandler(_dbContext, userContext));
    }

    [Fact]
    public async Task Create_ValidSubCategory_Succeeds()
    {
        var (create, _) = CreateHandlers();
        SubCategory? captured = null;
        _dbContext.SubCategories.When(x => x.Add(Arg.Any<SubCategory>()))
            .Do(x => captured = x.Arg<SubCategory>());

        var result = await create.Handle(new CreateSubCategoryCommand("bills", "Xăng"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        captured.ShouldNotBeNull();
        captured.Category.ShouldBe("bills");
        captured.Name.ShouldBe("Xăng");
        captured.UserId.ShouldBe(UserId);
    }

    [Fact]
    public async Task Create_UnknownParentCategory_Fails()
    {
        var (create, _) = CreateHandlers();

        var result = await create.Handle(new CreateSubCategoryCommand("fuel", "Xăng"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SubCategoryErrors.InvalidCategory);
    }

    [Fact]
    public async Task Create_EmptyName_Fails()
    {
        var (create, _) = CreateHandlers();

        var result = await create.Handle(new CreateSubCategoryCommand("bills", "  "), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SubCategoryErrors.NameRequired);
    }

    [Fact]
    public async Task Create_Duplicate_Fails()
    {
        SubCategory existing = SubCategory.Create(UserId, "bills", "Xăng").Value;
        var (create, _) = CreateHandlers(existing);

        var result = await create.Handle(new CreateSubCategoryCommand("bills", "Xăng"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SubCategoryErrors.Duplicate);
    }

    [Fact]
    public async Task Get_FiltersByCategoryAndOwner()
    {
        SubCategory mine = SubCategory.Create(UserId, "bills", "Xăng").Value;
        SubCategory otherCategory = SubCategory.Create(UserId, "food", "Cà phê").Value;
        SubCategory foreign = SubCategory.Create(Guid.NewGuid(), "bills", "Dầu").Value;
        var (_, get) = CreateHandlers(mine, otherCategory, foreign);

        var result = await get.Handle(new GetSubCategoriesQuery("bills"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(1);
        result.Value[0].Name.ShouldBe("Xăng");
    }
}
