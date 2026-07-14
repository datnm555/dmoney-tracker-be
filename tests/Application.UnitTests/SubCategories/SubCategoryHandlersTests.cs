using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.SubCategories;
using Domain.Categories;
using Domain.SubCategories;
using Domain.Users;
using MockQueryable.NSubstitute;
using NSubstitute;
using Shouldly;

namespace Application.UnitTests.SubCategories;

public class SubCategoryHandlersTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Category Bills = Category.Create("Hóa đơn", "zap", "tester", "bills").Value;
    private static readonly Category Food = Category.Create("Ăn hàng", "utensils", "tester", "food").Value;

    private IApplicationDbContext _dbContext = null!;

    private (CreateSubCategoryCommandHandler create, GetSubCategoriesQueryHandler get) CreateHandlers(
        params SubCategory[] existing)
    {
        _dbContext = Substitute.For<IApplicationDbContext>();
        var userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        var subCategories = existing.ToList().BuildMockDbSet();
        _dbContext.SubCategories.Returns(subCategories);
        var categories = new List<Category> { Bills, Food }.BuildMockDbSet();
        _dbContext.Categories.Returns(categories);
        var users = new List<User> { UserWithId(UserId) }.BuildMockDbSet();
        _dbContext.Users.Returns(users);
        return (
            new CreateSubCategoryCommandHandler(_dbContext, userContext),
            new GetSubCategoriesQueryHandler(_dbContext, userContext));
    }

    private static User UserWithId(Guid id)
    {
        User user = User.Create("t@example.com", "tester", "Tester", "hash").Value;
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, id);
        return user;
    }

    [Fact]
    public async Task Create_ValidSubCategory_Succeeds()
    {
        var (create, _) = CreateHandlers();
        SubCategory? captured = null;
        _dbContext.SubCategories.When(x => x.Add(Arg.Any<SubCategory>()))
            .Do(x => captured = x.Arg<SubCategory>());

        var result = await create.Handle(new CreateSubCategoryCommand(Bills.Id, "Xăng"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        captured.ShouldNotBeNull();
        captured.CategoryId.ShouldBe(Bills.Id);
        captured.Name.ShouldBe("Xăng");
        captured.CreatedBy.ShouldBe("tester");
    }

    [Fact]
    public async Task Create_UnknownParentCategory_Fails()
    {
        var (create, _) = CreateHandlers();

        var result = await create.Handle(new CreateSubCategoryCommand(Guid.NewGuid(), "Xăng"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SubCategoryErrors.InvalidCategory);
    }

    [Fact]
    public async Task Create_EmptyName_Fails()
    {
        var (create, _) = CreateHandlers();

        var result = await create.Handle(new CreateSubCategoryCommand(Bills.Id, "  "), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SubCategoryErrors.NameRequired);
    }

    [Fact]
    public async Task Create_Duplicate_Fails()
    {
        SubCategory existing = SubCategory.Create(Bills.Id, "Xăng", "tester").Value;
        var (create, _) = CreateHandlers(existing);

        var result = await create.Handle(new CreateSubCategoryCommand(Bills.Id, "Xăng"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SubCategoryErrors.Duplicate);
    }

    [Fact]
    public async Task Get_FiltersByCategory()
    {
        SubCategory billsSub = SubCategory.Create(Bills.Id, "Xăng", "tester").Value;
        SubCategory foodSub = SubCategory.Create(Food.Id, "Cà phê", "tester").Value;
        var (_, get) = CreateHandlers(billsSub, foodSub);

        var result = await get.Handle(new GetSubCategoriesQuery(Bills.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(1);
        result.Value[0].Name.ShouldBe("Xăng");
        result.Value[0].CategoryId.ShouldBe(Bills.Id);
    }

    [Fact]
    public async Task Create_NewDefault_UnsetsThePreviousDefault()
    {
        SubCategory oldDefault = SubCategory.Create(Bills.Id, "Xăng", "tester", true).Value;
        var (create, _) = CreateHandlers(oldDefault);
        SubCategory? captured = null;
        _dbContext.SubCategories.When(x => x.Add(Arg.Any<SubCategory>()))
            .Do(x => captured = x.Arg<SubCategory>());

        var result = await create.Handle(
            new CreateSubCategoryCommand(Bills.Id, "Dầu", true), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        captured.ShouldNotBeNull();
        captured.IsDefault.ShouldBeTrue();
        oldDefault.IsDefault.ShouldBeFalse();
    }
}
