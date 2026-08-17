using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Api.IntegrationTests.Transactions;

public sealed class StatsEndpointTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private static readonly string ThisMonth =
        DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string username)
    {
        HttpClient client = factory.CreateClient();
        var register = await client.PostAsJsonAsync("/users/register",
            new { email, username, displayName = "Stats User", password = "password123" });
        register.StatusCode.ShouldBe(HttpStatusCode.OK);
        var login = await client.PostAsJsonAsync("/users/login",
            new { identifier = email, password = "password123" });
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await login.Content.ReadFromJsonAsync<LoginBody>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    private static object Payload(string date, decimal credit, decimal debit, Guid? categoryId, Guid planId) => new
    {
        date,
        content = "stats tx",
        creditAmount = credit,
        debitAmount = debit,
        note = (string?)null,
        categoryId,
        planId
    };

    private static async Task<Guid> CreateCategoryAsync(HttpClient client, string name, string icon)
    {
        var response = await client.PostAsJsonAsync("/categories", new { name, icon });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    private static async Task<Guid> GetDefaultPlanIdAsync(HttpClient client)
    {
        var plans = await (await client.GetAsync("/plans")).Content.ReadFromJsonAsync<List<PlanListBody>>();
        return plans![0].Id;
    }

    internal sealed record PlanListBody(Guid Id, string Name, bool IsDefault);

    internal sealed record CreatedBody(Guid Id);

    [Fact]
    public async Task Stats_WithoutToken_Returns401()
    {
        HttpClient anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync($"/transactions/stats?month={ThisMonth}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Stats_WithInvalidMonth_Returns400()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("statsbad@example.com", "statsbad");

        var response = await client.GetAsync("/transactions/stats?month=garbage");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Stats_AggregatesMonthlyDailyAndCategory()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("stats@example.com", "statsuser");
        Guid defaultPlan = await GetDefaultPlanIdAsync(client);

        Guid salaryId = await CreateCategoryAsync(client, "Lương stats", "wallet");
        Guid foodId = await CreateCategoryAsync(client, "Ăn hàng stats", "utensils");
        (await client.PostAsJsonAsync("/transactions",
            Payload($"{ThisMonth}-05", 15_000_000m, 0m, salaryId, defaultPlan))).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await client.PostAsJsonAsync("/transactions",
            Payload($"{ThisMonth}-10", 0m, 200_000m, foodId, defaultPlan))).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await client.PostAsJsonAsync("/transactions",
            Payload($"{ThisMonth}-10", 0m, 50_000m, salaryId, defaultPlan))).StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await client.GetAsync($"/transactions/stats?month={ThisMonth}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stats = await response.Content.ReadFromJsonAsync<StatsBody>();
        stats.ShouldNotBeNull();

        stats.Monthly.Count.ShouldBe(12);
        StatsBody.MonthlyItem current = stats.Monthly[^1];
        current.Month.ShouldBe(ThisMonth);
        current.TotalCredit.Amount.ShouldBe(15_000_000m);
        current.TotalDebit.Amount.ShouldBe(250_000m);
        current.Balance.Amount.ShouldBe(14_750_000m);

        stats.Daily.Count.ShouldBe(1);
        stats.Daily[0].Day.ShouldBe(10);
        stats.Daily[0].Debit.Amount.ShouldBe(250_000m);

        stats.ByCategory.Count.ShouldBe(2);
        stats.ByCategory[0].CategoryId.ShouldBe(foodId);
        stats.ByCategory[1].CategoryId.ShouldBe(salaryId);
        stats.ByCategory[1].Debit.Amount.ShouldBe(50_000m);
    }

    internal sealed record LoginBody(string Token, Guid UserId, string Email, string Username, string DisplayName);
    internal sealed record MoneyBody(decimal Amount, string Currency);

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
