using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Categories;
using Domain.Categories;
using Domain.SubCategories;
using Domain.Transactions;
using Domain.Users;
using MockQueryable.NSubstitute;
using NSubstitute;
using Shouldly;

namespace Application.UnitTests.Categories;

public class CategoryHandlersTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private IApplicationDbContext _dbContext = null!;

    private (CreateCategoryCommandHandler create, GetCategoriesQueryHandler get, DeleteCategoryCommandHandler delete)
        CreateHandlers(
            Category[]? categories = null,
            Transaction[]? transactions = null,
            SubCategory[]? subCategories = null)
    {
        _dbContext = Substitute.For<IApplicationDbContext>();
        var userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        var categoriesDbSet = (categories ?? []).ToList().BuildMockDbSet();
        _dbContext.Categories.Returns(categoriesDbSet);
        var transactionsDbSet = (transactions ?? []).ToList().BuildMockDbSet();
        _dbContext.Transactions.Returns(transactionsDbSet);
        var subCategoriesDbSet = (subCategories ?? []).ToList().BuildMockDbSet();
        _dbContext.SubCategories.Returns(subCategoriesDbSet);
        var users = new List<User> { UserWithId(UserId) }.BuildMockDbSet();
        _dbContext.Users.Returns(users);
        return (
            new CreateCategoryCommandHandler(_dbContext, userContext),
            new GetCategoriesQueryHandler(_dbContext, userContext),
            new DeleteCategoryCommandHandler(_dbContext, userContext));
    }

    private static User UserWithId(Guid id)
    {
        User user = User.Create("t@example.com", "tester", "Tester", "hash").Value;
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, id);
        return user;
    }

    [Fact]
    public async Task Create_ValidCategory_StampsCreatedByWithUsername()
    {
        var (create, _, _) = CreateHandlers();
        Category? captured = null;
        _dbContext.Categories.When(x => x.Add(Arg.Any<Category>()))
            .Do(x => captured = x.Arg<Category>());

        var result = await create.Handle(new CreateCategoryCommand("Du lịch", "plane"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        captured.ShouldNotBeNull();
        captured.Name.ShouldBe("Du lịch");
        captured.Icon.ShouldBe("plane");
        captured.CreatedBy.ShouldBe("tester");
        captured.UpdatedBy.ShouldBe("tester");
    }

    [Fact]
    public async Task Create_MissingIcon_Fails()
    {
        var (create, _, _) = CreateHandlers();

        var result = await create.Handle(new CreateCategoryCommand("Du lịch", " "), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(CategoryErrors.IconRequired);
    }

    [Fact]
    public async Task Create_DuplicateName_Fails()
    {
        Category existing = Category.Create("Du lịch", "plane", "tester").Value;
        var (create, _, _) = CreateHandlers([existing]);

        var result = await create.Handle(new CreateCategoryCommand("Du lịch", "gift"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(CategoryErrors.Duplicate);
    }

    [Fact]
    public async Task Get_ReturnsSharedCategories_SystemFirst()
    {
        Category custom = Category.Create("Du lịch", "plane", "tester").Value;
        Category system = Category.Create("Hóa đơn", "zap", "tester", "bills").Value;
        var (_, get, _) = CreateHandlers([custom, system]);

        var result = await get.Handle(new GetCategoriesQuery(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
        result.Value[0].Code.ShouldBe("bills");
        result.Value[1].Name.ShouldBe("Du lịch");
        result.Value[1].Code.ShouldBeNull();
    }

    [Fact]
    public async Task Delete_CategoryUsedByTransaction_Fails()
    {
        Category category = Category.Create("Du lịch", "plane", "tester").Value;
        Transaction usage = Transaction.Create(
            UserId, new DateOnly(2026, 7, 1), "Vé máy bay",
            Money.Zero(), Money.Create(2_000_000m).Value, null,
            category.Id).Value;
        var (_, _, delete) = CreateHandlers([category], [usage]);

        var result = await delete.Handle(new DeleteCategoryCommand(category.Id), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(CategoryErrors.InUse);
    }

    [Fact]
    public async Task Delete_CategoryUsedBySubCategory_Fails()
    {
        Category category = Category.Create("Du lịch", "plane", "tester").Value;
        SubCategory sub = SubCategory.Create(category.Id, "Vé", "tester").Value;
        var (_, _, delete) = CreateHandlers([category], subCategories: [sub]);

        var result = await delete.Handle(new DeleteCategoryCommand(category.Id), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(CategoryErrors.InUse);
    }

    [Fact]
    public async Task Delete_UnusedCategory_Succeeds()
    {
        Category category = Category.Create("Du lịch", "plane", "tester").Value;
        var (_, _, delete) = CreateHandlers([category]);

        var result = await delete.Handle(new DeleteCategoryCommand(category.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _dbContext.Categories.Received(1).Remove(category);
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
