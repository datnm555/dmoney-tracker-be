using Api.IntegrationTests.Infrastructure;
using Domain.Categories;
using Domain.SubCategories;
using global::Infrastructure.Database;
using global::Infrastructure.Database.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Api.IntegrationTests.SubCategories;

/// <summary>
/// Replays the AddSubCategoryCode data step against seeded data (the migration itself runs
/// on an empty db, where it must be a no-op).
/// </summary>
public sealed class SavingsBookMigrationTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task SeedSavingsBookSql_NoSavingsCategory_IsNoOp_ThenCreates_ThenTagsExistingByName()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Fresh db (migration already ran): no savings category, nothing inserted.
        await db.Database.ExecuteSqlRawAsync(AddSubCategoryCode.SeedSavingsBookSql);
        (await db.SubCategories.CountAsync()).ShouldBe(0);

        // Savings category exists, no "Sổ tiết kiệm" → inserted with the code.
        Category savings = Category.Create("Tích luỹ", "piggy-bank", "system", "savings", CategoryKinds.Both).Value;
        db.Categories.Add(savings);
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync(AddSubCategoryCode.SeedSavingsBookSql);
        await db.Database.ExecuteSqlRawAsync(AddSubCategoryCode.SeedSavingsBookSql);

        List<SubCategory> created = await db.SubCategories.AsNoTracking().ToListAsync();
        created.Count.ShouldBe(1);
        created[0].CategoryId.ShouldBe(savings.Id);
        created[0].Name.ShouldBe("Sổ tiết kiệm");
        created[0].Code.ShouldBe(SubCategoryCodes.SavingsBook);
        created[0].IsDefault.ShouldBeFalse();
        created[0].CreatedBy.ShouldBe("system");

        // An existing uncoded "Sổ tiết kiệm" gets tagged instead of a new row being inserted.
        await db.Database.ExecuteSqlRawAsync("DELETE FROM sub_categories");
        SubCategory existing = SubCategory.Create(savings.Id, "Sổ tiết kiệm", "someone").Value;
        db.SubCategories.Add(existing);
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync(AddSubCategoryCode.SeedSavingsBookSql);

        List<SubCategory> tagged = await db.SubCategories.AsNoTracking().ToListAsync();
        tagged.Count.ShouldBe(1);
        tagged[0].Id.ShouldBe(existing.Id);
        tagged[0].Code.ShouldBe(SubCategoryCodes.SavingsBook);
    }
}
