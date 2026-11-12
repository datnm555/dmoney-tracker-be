using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Api.IntegrationTests.Transactions;

/// <summary>
/// Categories / sub-categories flagged ExcludeFromStats: their transactions still list
/// (with excludedFromStats=true) but are left out of every total and chart.
/// Categories are global, so every name carries a unique suffix.
/// </summary>
public sealed class ExcludeFromStatsEndpointTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private static readonly string ThisMonth =
        DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..30];

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        string tag = Guid.NewGuid().ToString("N")[..12];
        string email = $"excl-{tag}@example.com";
        HttpClient client = factory.CreateClient();
        var register = await client.PostAsJsonAsync("/users/register",
            new { email, username = $"excl{tag}", displayName = "Excl User", password = "password123" });
        register.StatusCode.ShouldBe(HttpStatusCode.OK);
        var login = await client.PostAsJsonAsync("/users/login",
            new { identifier = email, password = "password123" });
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await login.Content.ReadFromJsonAsync<LoginBody>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    private static async Task<Guid> GetDefaultPlanIdAsync(HttpClient client)
    {
        var plans = await (await client.GetAsync("/plans")).Content.ReadFromJsonAsync<List<PlanListBody>>();
        return plans!.Single(p => p.IsDefault).Id;
    }

    private static async Task<Guid> CreateCategoryAsync(HttpClient client, string name, bool excludeFromStats)
    {
        var response = await client.PostAsJsonAsync("/categories", new { name, icon = "tag", excludeFromStats });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    private static async Task<Guid> CreateSubCategoryAsync(
        HttpClient client, Guid categoryId, string name, bool excludeFromStats)
    {
        var response = await client.PostAsJsonAsync("/subcategories", new { categoryId, name, excludeFromStats });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    private static async Task<Guid> CreateTransactionAsync(
        HttpClient client, Guid planId, int day, decimal credit, decimal debit, Guid categoryId, Guid? subCategoryId)
    {
        var response = await client.PostAsJsonAsync("/transactions", new
        {
            date = $"{ThisMonth}-{day:00}",
            content = "excl tx",
            creditAmount = credit,
            debitAmount = debit,
            categoryId,
            subCategoryId,
            planId
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    /// <summary>
    /// Seeds one counted credit + one counted debit, and flagged ones via a flagged
    /// category and via a flagged sub-category of an unflagged category.
    /// </summary>
    private static async Task<Seed> SeedAsync(HttpClient client)
    {
        Guid planId = await GetDefaultPlanIdAsync(client);
        Guid flaggedCat = await CreateCategoryAsync(client, Unique("Chuyển nội bộ"), excludeFromStats: true);
        Guid normalCat = await CreateCategoryAsync(client, Unique("Nợ"), excludeFromStats: false);
        Guid flaggedSub = await CreateSubCategoryAsync(client, normalCat, "Cho vay", excludeFromStats: true);
        Guid normalSub = await CreateSubCategoryAsync(client, normalCat, "Lãi vay", excludeFromStats: false);

        List<Guid> counted =
        [
            await CreateTransactionAsync(client, planId, 3, 1_000_000m, 0m, normalCat, normalSub),
            await CreateTransactionAsync(client, planId, 10, 0m, 200_000m, normalCat, normalSub)
        ];
        List<Guid> excluded =
        [
            await CreateTransactionAsync(client, planId, 4, 5_000_000m, 0m, flaggedCat, null),
            await CreateTransactionAsync(client, planId, 11, 0m, 300_000m, flaggedCat, null),
            await CreateTransactionAsync(client, planId, 5, 7_000_000m, 0m, normalCat, flaggedSub),
            await CreateTransactionAsync(client, planId, 12, 0m, 400_000m, normalCat, flaggedSub)
        ];

        return new Seed(planId, normalCat, counted, excluded);
    }

    [Fact]
    public async Task DashboardStats_ExcludeFlaggedCategoryAndSubCategory()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await SeedAsync(client);

        var stats = await client.GetFromJsonAsync<StatsBody>(
            $"/transactions/stats?month={ThisMonth}&planId={seed.PlanId}");

        stats.ShouldNotBeNull();
        StatsBody.MonthlyItem current = stats.Monthly[^1];
        current.Month.ShouldBe(ThisMonth);
        current.TotalCredit.Amount.ShouldBe(1_000_000m);
        current.TotalDebit.Amount.ShouldBe(200_000m);
        current.Balance.Amount.ShouldBe(800_000m);

        stats.Daily.Count.ShouldBe(1);
        stats.Daily[0].Day.ShouldBe(10);
        stats.Daily[0].Debit.Amount.ShouldBe(200_000m);

        stats.ByCategory.Count.ShouldBe(1);
        stats.ByCategory[0].CategoryId.ShouldBe(seed.NormalCategoryId);
        stats.ByCategory[0].Debit.Amount.ShouldBe(200_000m);
    }

    [Fact]
    public async Task MonthSummary_TotalsExcludeFlagged_ItemsStillListWithFlag()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await SeedAsync(client);

        var summary = await client.GetFromJsonAsync<SummaryBody>(
            $"/transactions?month={ThisMonth}&planId={seed.PlanId}");

        summary.ShouldNotBeNull();
        summary.TotalCredit.Amount.ShouldBe(1_000_000m);
        summary.TotalDebit.Amount.ShouldBe(200_000m);
        summary.Balance.Amount.ShouldBe(800_000m);

        summary.Items.Count.ShouldBe(6);
        foreach (Guid id in seed.Excluded)
        {
            summary.Items.Single(i => i.Id == id).ExcludedFromStats.ShouldBeTrue();
        }

        foreach (Guid id in seed.Counted)
        {
            summary.Items.Single(i => i.Id == id).ExcludedFromStats.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task Category_ExcludeFromStats_RoundTripsThroughCreateAndUpdate()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        string name = Unique("Cat excl");
        Guid id = await CreateCategoryAsync(client, name, excludeFromStats: true);

        (await GetCategoryAsync(client, id)).ExcludeFromStats.ShouldBeTrue();

        (await client.PutAsJsonAsync($"/categories/{id}", new { name, icon = "tag", excludeFromStats = false }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetCategoryAsync(client, id)).ExcludeFromStats.ShouldBeFalse();

        (await client.PutAsJsonAsync($"/categories/{id}", new { name, icon = "tag", excludeFromStats = true }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetCategoryAsync(client, id)).ExcludeFromStats.ShouldBeTrue();

        Guid plain = await CreateCategoryAsync(client, Unique("Cat plain"), excludeFromStats: false);
        (await GetCategoryAsync(client, plain)).ExcludeFromStats.ShouldBeFalse();
    }

    [Fact]
    public async Task SubCategory_ExcludeFromStats_RoundTripsThroughCreateAndUpdate()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid categoryId = await CreateCategoryAsync(client, Unique("Sub parent"), excludeFromStats: false);
        Guid id = await CreateSubCategoryAsync(client, categoryId, "Thu hồi nợ", excludeFromStats: true);

        (await GetSubCategoryAsync(client, categoryId, id)).ExcludeFromStats.ShouldBeTrue();

        (await client.PutAsJsonAsync($"/subcategories/{id}", new { name = "Thu hồi nợ", excludeFromStats = false }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetSubCategoryAsync(client, categoryId, id)).ExcludeFromStats.ShouldBeFalse();

        (await client.PutAsJsonAsync($"/subcategories/{id}", new { name = "Thu hồi nợ", excludeFromStats = true }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetSubCategoryAsync(client, categoryId, id)).ExcludeFromStats.ShouldBeTrue();

        Guid plain = await CreateSubCategoryAsync(client, categoryId, "Vay vào", excludeFromStats: false);
        (await GetSubCategoryAsync(client, categoryId, plain)).ExcludeFromStats.ShouldBeFalse();
    }

    [Fact]
    public async Task Stats_ReflectFlagToggledViaUpdate()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid planId = await GetDefaultPlanIdAsync(client);
        string name = Unique("Toggle");
        Guid categoryId = await CreateCategoryAsync(client, name, excludeFromStats: false);
        await CreateTransactionAsync(client, planId, 8, 0m, 150_000m, categoryId, null);

        (await client.PutAsJsonAsync($"/categories/{categoryId}", new { name, icon = "tag", excludeFromStats = true }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var summary = await client.GetFromJsonAsync<SummaryBody>($"/transactions?month={ThisMonth}&planId={planId}");
        summary!.TotalDebit.Amount.ShouldBe(0m);
        summary.Items.Single().ExcludedFromStats.ShouldBeTrue();
    }

    [Fact]
    public async Task Stats_LeaveOutAdvancesGoldAndTheRepaidPartOfReimbursements()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid planId = await GetDefaultPlanIdAsync(client);
        Guid categoryId = await CreateCategoryAsync(client, Unique("Advance cat"), excludeFromStats: false);
        await CreateTransactionAsync(client, planId, 2, 0m, 100_000m, categoryId, null);

        Guid advanceId = await PostTransactionAsync(client, new
        {
            date = $"{ThisMonth}-03", content = "ứng hộ", creditAmount = 0m, debitAmount = 300_000m,
            categoryId, planId, isAdvance = true
        });
        Guid reimburseId = await PostTransactionAsync(client, new
        {
            date = $"{ThisMonth}-04", content = "được trả + thêm", creditAmount = 500_000m, debitAmount = 0m,
            categoryId, planId, advanceTransactionIds = new[] { advanceId }
        });

        var goldType = await client.PostAsJsonAsync("/gold-types", new { name = Unique("SJC") });
        goldType.StatusCode.ShouldBe(HttpStatusCode.Created);
        Guid goldTypeId = (await goldType.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
        Guid goldId = await PostTransactionAsync(client, new
        {
            date = $"{ThisMonth}-05", content = "mua vàng", creditAmount = 0m, debitAmount = 8_000_000m,
            categoryId, planId, goldTypeId, goldQuantity = 1m
        });

        var summary = await client.GetFromJsonAsync<SummaryBody>($"/transactions?month={ThisMonth}&planId={planId}");
        summary.ShouldNotBeNull();
        summary.TotalCredit.Amount.ShouldBe(200_000m);
        summary.TotalDebit.Amount.ShouldBe(100_000m);
        summary.Items.Single(i => i.Id == advanceId).ExcludedFromStats.ShouldBeTrue();
        summary.Items.Single(i => i.Id == goldId).ExcludedFromStats.ShouldBeTrue();
        ItemBody reimbursement = summary.Items.Single(i => i.Id == reimburseId);
        reimbursement.ExcludedFromStats.ShouldBeFalse();
        reimbursement.CountedCredit!.Amount.ShouldBe(200_000m);

        var stats = await client.GetFromJsonAsync<StatsBody>($"/transactions/stats?month={ThisMonth}&planId={planId}");
        stats!.Monthly[^1].TotalCredit.Amount.ShouldBe(200_000m);
        stats.Monthly[^1].TotalDebit.Amount.ShouldBe(100_000m);
        stats.Daily.Single().Day.ShouldBe(2);
        stats.ByCategory.Single().Debit.Amount.ShouldBe(100_000m);
    }

    private static async Task<Guid> PostTransactionAsync(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/transactions", body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    private static async Task<CategoryBody> GetCategoryAsync(HttpClient client, Guid id)
    {
        var list = await client.GetFromJsonAsync<List<CategoryBody>>("/categories");
        return list!.Single(c => c.Id == id);
    }

    private static async Task<SubCategoryBody> GetSubCategoryAsync(HttpClient client, Guid categoryId, Guid id)
    {
        var list = await client.GetFromJsonAsync<List<SubCategoryBody>>($"/subcategories?categoryId={categoryId}");
        return list!.Single(s => s.Id == id);
    }

    private sealed record Seed(Guid PlanId, Guid NormalCategoryId, List<Guid> Counted, List<Guid> Excluded);

    internal sealed record LoginBody(string Token, Guid UserId, string Email, string Username, string DisplayName);
    internal sealed record PlanListBody(Guid Id, string Name, bool IsDefault);
    internal sealed record CreatedBody(Guid Id);
    internal sealed record MoneyBody(decimal Amount, string Currency);
    internal sealed record CategoryBody(Guid Id, string Name, string Icon, string? Code, string Kind, bool ExcludeFromStats);
    internal sealed record SubCategoryBody(
        Guid Id, Guid CategoryId, string Name, bool IsDefault, string? Icon, string? Code, bool ExcludeFromStats);
    internal sealed record ItemBody(
        Guid Id, string Content, MoneyBody Credit, MoneyBody Debit, bool ExcludedFromStats, MoneyBody? CountedCredit = null);
    internal sealed record SummaryBody(List<ItemBody> Items, MoneyBody TotalCredit, MoneyBody TotalDebit, MoneyBody Balance);

    internal sealed record StatsBody(
        List<StatsBody.MonthlyItem> Monthly,
        List<StatsBody.DailyItem> Daily,
        List<StatsBody.CategoryItem> ByCategory)
    {
        internal sealed record MonthlyItem(string Month, MoneyBody TotalCredit, MoneyBody TotalDebit, MoneyBody Balance);
        internal sealed record DailyItem(int Day, MoneyBody Debit);
        internal sealed record CategoryItem(Guid? CategoryId, MoneyBody Debit);
    }
}
