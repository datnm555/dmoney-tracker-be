using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.IntegrationTests.Infrastructure;
using Domain.Categories;
using Domain.SubCategories;
using global::Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Api.IntegrationTests.Savings;

public sealed class SavingsSummaryEndpointTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string username)
    {
        HttpClient client = factory.CreateClient();
        var register = await client.PostAsJsonAsync("/users/register",
            new { email, username, displayName = "Test User", password = "password123" });
        register.StatusCode.ShouldBe(HttpStatusCode.OK);

        var login = await client.PostAsJsonAsync("/users/login",
            new { identifier = email, password = "password123" });
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await login.Content.ReadFromJsonAsync<LoginBody>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    /// <summary>
    /// A fresh test db has no seeded (coded) global categories and the API cannot create one,
    /// so seed it directly (idempotent), then resolve its id through GET /categories.
    /// </summary>
    private async Task<Guid> GetCategoryIdByCodeAsync(HttpClient client, string code)
    {
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (!await dbContext.Categories.AnyAsync(c => c.Code == code))
            {
                string kind = code == "savings" ? CategoryKinds.Both : CategoryKinds.Expense;
                dbContext.Categories.Add(Category.Create(code, "tag", "test", code, kind).Value);
                await dbContext.SaveChangesAsync();
            }
        }

        var categories = await (await client.GetAsync("/categories"))
            .Content.ReadFromJsonAsync<List<CategoryBody>>();
        return categories!.Single(c => c.Code == code).Id;
    }

    /// <summary>
    /// Seeds the coded "Sổ tiết kiệm" (savings_book) sub-category under the savings category
    /// directly (fresh test dbs have neither; the API cannot create coded rows). Idempotent.
    /// </summary>
    private async Task<Guid> GetSavingsBookIdAsync(HttpClient client)
    {
        Guid savingsId = await GetCategoryIdByCodeAsync(client, "savings");
        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        SubCategory? book = await dbContext.SubCategories
            .FirstOrDefaultAsync(s => s.Code == SubCategoryCodes.SavingsBook);
        if (book is null)
        {
            book = SubCategory.Create(savingsId, "Sổ tiết kiệm", "system", code: SubCategoryCodes.SavingsBook).Value;
            dbContext.SubCategories.Add(book);
            await dbContext.SaveChangesAsync();
        }

        return book.Id;
    }

    private static async Task<Guid> CreateSubCategoryAsync(HttpClient client, Guid categoryId, string name)
    {
        var response = await client.PostAsJsonAsync("/subcategories", new { categoryId, name });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    private static async Task<Guid> GetDefaultPlanIdAsync(HttpClient client)
    {
        var plans = await (await client.GetAsync("/plans")).Content.ReadFromJsonAsync<List<PlanListBody>>();
        return plans!.Single(p => p.IsDefault).Id;
    }

    private static async Task<Guid> CreatePlanAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/plans", new { name });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    private static async Task PostTxAsync(
        HttpClient client, Guid planId, Guid categoryId, Guid? subCategoryId, string date, string content,
        decimal credit, decimal debit, string? savingsBank, string? bank = null) =>
        (await client.PostAsJsonAsync("/transactions", new
        {
            date, content, creditAmount = credit, debitAmount = debit,
            note = (string?)null, categoryId, subCategoryId, planId, bank, savingsBank
        })).StatusCode.ShouldBe(HttpStatusCode.Created);

    internal sealed record LoginBody(string Token, Guid UserId, string Email, string Username, string DisplayName);
    internal sealed record CreatedBody(Guid Id);
    internal sealed record PlanListBody(Guid Id, string Name, bool IsDefault);
    internal sealed record CategoryBody(Guid Id, string Name, string Icon, string? Code, string Kind);
    internal sealed record MoneyBody(decimal Amount, string Currency);

    internal sealed record SavingsSummaryBody(
        Guid PlanId,
        string PlanName,
        MoneyBody TotalBalance,
        List<SavingsSummaryBody.AccountItem> Accounts,
        List<SavingsSummaryBody.TxItem> Transactions)
    {
        internal sealed record AccountItem(
            string? SavingsBank, MoneyBody Balance, MoneyBody TotalDeposited, MoneyBody TotalWithdrawn,
            int TransactionCount);

        internal sealed record TxItem(
            Guid TransactionId, string Date, string Content, string? SavingsBank, string? Bank,
            MoneyBody Credit, MoneyBody Debit, Guid PlanId, string? SubCategoryName, string? Note);
    }

    [Fact]
    public async Task SavingsSummary_WithoutToken_Returns401()
    {
        (await factory.CreateClient().GetAsync("/savings/summary")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SavingsSummary_Empty_ReturnsDefaultPlanAndZeroTotal()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("sav-empty@example.com", "savempty");
        Guid defaultPlan = await GetDefaultPlanIdAsync(client);

        var response = await client.GetAsync("/savings/summary");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SavingsSummaryBody>();

        body!.PlanId.ShouldBe(defaultPlan);
        body.PlanName.ShouldBe("Sổ chính");
        body.TotalBalance.Amount.ShouldBe(0m);
        body.TotalBalance.Currency.ShouldBe("VND");
        body.Accounts.ShouldBeEmpty();
        body.Transactions.ShouldBeEmpty();
    }

    [Fact]
    public async Task SavingsSummary_OnlyDefaultPlanSavingsBookRows_GroupedPerSavingsBank()
    {
        HttpClient other = await CreateAuthenticatedClientAsync("sav-other@example.com", "savother");
        await PostTxAsync(other, await GetDefaultPlanIdAsync(other), await GetCategoryIdByCodeAsync(other, "savings"),
            await GetSavingsBookIdAsync(other), "2026-08-01", "Người khác gửi", 0m, 50_000_000m, "Vietinbank");

        HttpClient client = await CreateAuthenticatedClientAsync("sav-sum@example.com", "savsum");
        Guid savings = await GetCategoryIdByCodeAsync(client, "savings");
        Guid book = await GetSavingsBookIdAsync(client);
        Guid gold = await CreateSubCategoryAsync(client, savings, "Vàng");
        Guid food = await GetCategoryIdByCodeAsync(client, "food");
        Guid defaultPlan = await GetDefaultPlanIdAsync(client);
        Guid otherPlan = await CreatePlanAsync(client, "Sổ phụ");

        await PostTxAsync(client, defaultPlan, savings, book, "2026-08-01", "Gửi 6tr", 0m, 6_000_000m, "Vietinbank", "VPBank");
        await PostTxAsync(client, otherPlan, savings, book, "2026-08-02", "Gửi 4tr sổ phụ", 0m, 4_000_000m, "Vietinbank");
        await PostTxAsync(client, defaultPlan, savings, book, "2026-08-03", "Rút 1tr", 1_000_000m, 0m, "Vietinbank");
        await PostTxAsync(client, defaultPlan, savings, book, "2026-08-04", "Gửi chưa rõ", 0m, 2_000_000m, null);
        await PostTxAsync(client, defaultPlan, savings, gold, "2026-08-05", "Mua vàng", 0m, 3_000_000m, "Vietinbank");
        await PostTxAsync(client, defaultPlan, savings, null, "2026-08-06", "Tích luỹ không sub", 0m, 5_000_000m, "Vietinbank");
        await PostTxAsync(client, defaultPlan, food, null, "2026-08-07", "Ăn trưa", 0m, 100_000m, "Vietinbank");

        var body = await (await client.GetAsync("/savings/summary")).Content.ReadFromJsonAsync<SavingsSummaryBody>();

        body!.PlanId.ShouldBe(defaultPlan);
        body.PlanName.ShouldBe("Sổ chính");
        body.TotalBalance.Amount.ShouldBe(7_000_000m);
        body.Accounts.Count.ShouldBe(2);

        SavingsSummaryBody.AccountItem vietin = body.Accounts[0];
        vietin.SavingsBank.ShouldBe("Vietinbank");
        vietin.Balance.Amount.ShouldBe(5_000_000m);
        vietin.TotalDeposited.Amount.ShouldBe(6_000_000m);
        vietin.TotalWithdrawn.Amount.ShouldBe(1_000_000m);
        vietin.TransactionCount.ShouldBe(2);

        SavingsSummaryBody.AccountItem unassigned = body.Accounts[1];
        unassigned.SavingsBank.ShouldBeNull();
        unassigned.Balance.Amount.ShouldBe(2_000_000m);
        unassigned.TotalDeposited.Amount.ShouldBe(2_000_000m);
        unassigned.TotalWithdrawn.Amount.ShouldBe(0m);
        unassigned.TransactionCount.ShouldBe(1);

        body.Transactions.Select(t => t.Content).ShouldBe(["Gửi chưa rõ", "Rút 1tr", "Gửi 6tr"]);
        SavingsSummaryBody.TxItem first = body.Transactions[2];
        first.Bank.ShouldBe("VPBank");
        first.SavingsBank.ShouldBe("Vietinbank");
        first.PlanId.ShouldBe(defaultPlan);
        first.SubCategoryName.ShouldBe("Sổ tiết kiệm");
        first.Debit.Amount.ShouldBe(6_000_000m);
    }

    [Fact]
    public async Task SavingsSummary_FollowsDefaultPlanSwitch()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("sav-switch@example.com", "savswitch");
        Guid savings = await GetCategoryIdByCodeAsync(client, "savings");
        Guid book = await GetSavingsBookIdAsync(client);
        Guid mainPlan = await GetDefaultPlanIdAsync(client);
        Guid familyPlan = await CreatePlanAsync(client, "Sổ gia đình");

        await PostTxAsync(client, mainPlan, savings, book, "2026-08-01", "Gửi sổ chính", 0m, 1_000_000m, "ACB");
        await PostTxAsync(client, familyPlan, savings, book, "2026-08-01", "Gửi sổ gia đình", 0m, 8_000_000m, "BIDV");

        (await client.PutAsync($"/plans/{familyPlan}/default", null)).IsSuccessStatusCode.ShouldBeTrue();

        var body = await (await client.GetAsync("/savings/summary")).Content.ReadFromJsonAsync<SavingsSummaryBody>();

        body!.PlanId.ShouldBe(familyPlan);
        body.PlanName.ShouldBe("Sổ gia đình");
        body.TotalBalance.Amount.ShouldBe(8_000_000m);
        body.Transactions.Select(t => t.Content).ShouldBe(["Gửi sổ gia đình"]);
    }

    [Fact]
    public async Task SavingsSummary_OrdersAccountsByBalanceDesc_UnassignedLast()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("sav-order@example.com", "savorder");
        Guid savings = await GetCategoryIdByCodeAsync(client, "savings");
        Guid book = await GetSavingsBookIdAsync(client);
        Guid plan = await GetDefaultPlanIdAsync(client);

        await PostTxAsync(client, plan, savings, book, "2026-08-01", "Không nơi gửi", 0m, 10_000_000m, null);
        await PostTxAsync(client, plan, savings, book, "2026-08-01", "ACB", 0m, 1_000_000m, "ACB");
        await PostTxAsync(client, plan, savings, book, "2026-08-01", "Techcom", 0m, 5_000_000m, "Techcombank");
        await PostTxAsync(client, plan, savings, book, "2026-08-02", "BIDV gửi", 0m, 1_000_000m, "BIDV");
        await PostTxAsync(client, plan, savings, book, "2026-08-03", "BIDV rút", 3_000_000m, 0m, "BIDV");

        var body = await (await client.GetAsync("/savings/summary")).Content.ReadFromJsonAsync<SavingsSummaryBody>();

        body!.Accounts.Select(a => a.SavingsBank).ShouldBe(["Techcombank", "ACB", "BIDV", null]);
        body.Accounts.Single(a => a.SavingsBank == "BIDV").Balance.Amount.ShouldBe(-2_000_000m);
        body.TotalBalance.Amount.ShouldBe(14_000_000m);
    }
}
