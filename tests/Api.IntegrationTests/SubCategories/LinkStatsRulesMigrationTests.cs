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
/// Replays the LinkStatsRules data step against seeded data (the migration itself runs on an
/// empty db, where it must be a no-op).
/// </summary>
public sealed class LinkStatsRulesMigrationTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task SeedSql_NoParents_IsNoOp_ThenTagsExistingAndCreatesMissing_AndExcludesSavings()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await db.Database.ExecuteSqlRawAsync(LinkStatsRules.SeedSql);
        (await db.SubCategories.CountAsync()).ShouldBe(0);

        Category transfers = Category.Create("Chuyển tiền nội bộ", "briefcase", "someone", null, CategoryKinds.Both).Value;
        Category debt = Category.Create("Nợ", "wallet", "someone", null, CategoryKinds.Both).Value;
        Category savings = Category.Create("Tích luỹ", "piggy-bank", "system", "savings", CategoryKinds.Both).Value;
        db.Categories.AddRange(transfers, debt, savings);
        SubCategory existingCardPayment = SubCategory.Create(debt.Id, "Trả thẻ tín dụng", "someone").Value;
        db.SubCategories.Add(existingCardPayment);
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync(LinkStatsRules.SeedSql);
        await db.Database.ExecuteSqlRawAsync(LinkStatsRules.SeedSql);

        List<SubCategory> subs = await db.SubCategories.AsNoTracking().ToListAsync();
        subs.Count.ShouldBe(2);
        SubCategory cardPayment = subs.Single(s => s.Code == SubCategoryCodes.CardPayment);
        cardPayment.Id.ShouldBe(existingCardPayment.Id);
        SubCategory transfer = subs.Single(s => s.Code == SubCategoryCodes.AccountTransfer);
        transfer.CategoryId.ShouldBe(transfers.Id);
        transfer.Name.ShouldBe("Chuyển giữa tài khoản");
        transfer.CreatedBy.ShouldBe("system");

        (await db.Categories.AsNoTracking().SingleAsync(c => c.Id == savings.Id)).ExcludeFromStats.ShouldBeTrue();
        (await db.Categories.AsNoTracking().SingleAsync(c => c.Id == debt.Id)).ExcludeFromStats.ShouldBeFalse();
    }
}
