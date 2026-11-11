using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Api.IntegrationTests.Transactions;

/// <summary>
/// GET /transactions?from=&amp;to= — an inclusive day range that takes precedence over ?month=,
/// with the same plan/user isolation as the month query.
/// </summary>
public sealed class TransactionsDateRangeEndpointTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        string tag = Guid.NewGuid().ToString("N")[..12];
        string email = $"range-{tag}@example.com";
        HttpClient client = factory.CreateClient();
        var register = await client.PostAsJsonAsync("/users/register",
            new { email, username = $"range{tag}", displayName = "Range User", password = "password123" });
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

    // Transactions need a category; xUnit builds a fresh instance per test, so this is one per test.
    private Guid? _categoryId;

    private async Task<Guid> GetCategoryIdAsync(HttpClient client)
    {
        if (_categoryId is { } id)
        {
            return id;
        }

        var response = await client.PostAsJsonAsync("/categories",
            new { name = $"Range {Guid.NewGuid():N}"[..20], icon = "tag" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        _categoryId = (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
        return _categoryId.Value;
    }

    private async Task CreateAsync(HttpClient client, Guid planId, string date, string content, decimal credit, decimal debit)
    {
        Guid categoryId = await GetCategoryIdAsync(client);
        var response = await client.PostAsJsonAsync("/transactions", new
        {
            date,
            content,
            creditAmount = credit,
            debitAmount = debit,
            note = (string?)null,
            categoryId,
            planId
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Range_IsInclusive_AndTotalsFollowIt()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid planId = await GetDefaultPlanIdAsync(client);
        await CreateAsync(client, planId, "2026-11-04", "Trước khoảng", 0m, 100_000m);
        await CreateAsync(client, planId, "2026-11-05", "Đầu khoảng", 0m, 200_000m);
        await CreateAsync(client, planId, "2026-11-12", "Giữa khoảng", 1_000_000m, 0m);
        await CreateAsync(client, planId, "2026-11-20", "Cuối khoảng", 0m, 300_000m);
        await CreateAsync(client, planId, "2026-11-21", "Sau khoảng", 0m, 400_000m);

        var summary = await client.GetFromJsonAsync<SummaryBody>(
            $"/transactions?from=2026-11-05&to=2026-11-20&planId={planId}");

        summary!.Items.Select(i => i.Content).ShouldBe(["Cuối khoảng", "Giữa khoảng", "Đầu khoảng"]);
        summary.TotalCredit.Amount.ShouldBe(1_000_000m);
        summary.TotalDebit.Amount.ShouldBe(500_000m);
        summary.Balance.Amount.ShouldBe(500_000m);
    }

    [Fact]
    public async Task Range_CanCrossMonths_AndOverridesMonth()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid planId = await GetDefaultPlanIdAsync(client);
        await CreateAsync(client, planId, "2026-10-30", "Cuối tháng 10", 0m, 10_000m);
        await CreateAsync(client, planId, "2026-11-02", "Đầu tháng 11", 0m, 20_000m);

        var summary = await client.GetFromJsonAsync<SummaryBody>(
            $"/transactions?month=2026-01&from=2026-10-25&to=2026-11-05&planId={planId}");

        summary!.Items.Count.ShouldBe(2);
        summary.TotalDebit.Amount.ShouldBe(30_000m);

        // The month query keeps working on its own.
        var month = await client.GetFromJsonAsync<SummaryBody>($"/transactions?month=2026-11&planId={planId}");
        month!.Items.Single().Content.ShouldBe("Đầu tháng 11");
    }

    [Fact]
    public async Task Range_SingleDay_IsAllowed()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid planId = await GetDefaultPlanIdAsync(client);
        await CreateAsync(client, planId, "2026-11-05", "Một ngày", 0m, 50_000m);
        await CreateAsync(client, planId, "2026-11-06", "Hôm sau", 0m, 60_000m);

        var summary = await client.GetFromJsonAsync<SummaryBody>(
            $"/transactions?from=2026-11-05&to=2026-11-05&planId={planId}");

        summary!.Items.Single().Content.ShouldBe("Một ngày");
    }

    [Fact]
    public async Task Range_IsScopedToUserAndPlan()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync();
        Guid ownerPlan = await GetDefaultPlanIdAsync(owner);
        await CreateAsync(owner, ownerPlan, "2026-11-10", "Của chủ", 0m, 70_000m);

        HttpClient other = await CreateAuthenticatedClientAsync();
        Guid otherPlan = await GetDefaultPlanIdAsync(other);

        // Someone else's plan is a 404, never their data.
        (await other.GetAsync($"/transactions?from=2026-11-01&to=2026-11-30&planId={ownerPlan}"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var own = await other.GetFromJsonAsync<SummaryBody>(
            $"/transactions?from=2026-11-01&to=2026-11-30&planId={otherPlan}");
        own!.Items.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("from=2026-11-20&to=2026-11-05", "Transactions.InvalidDateRange")] // from after to
    [InlineData("from=2026-11-05", "Transactions.InvalidDateRange")]               // missing to
    [InlineData("to=2026-11-05", "Transactions.InvalidDateRange")]                 // missing from
    [InlineData("from=05/11/2026&to=2026-11-20", "Transactions.InvalidDateRange")] // wrong format
    [InlineData("from=2026-02-30&to=2026-03-01", "Transactions.InvalidDateRange")] // not a date
    [InlineData("from=2025-01-01&to=2026-01-02", "Transactions.DateRangeTooLong")] // 367 days
    public async Task Range_Invalid_Returns400(string query, string code)
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid planId = await GetDefaultPlanIdAsync(client);

        var response = await client.GetAsync($"/transactions?{query}&planId={planId}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain(code);
    }

    [Fact]
    public async Task Range_OfExactly366Days_IsAllowed()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid planId = await GetDefaultPlanIdAsync(client);

        (await client.GetAsync($"/transactions?from=2024-01-01&to=2024-12-31&planId={planId}"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    internal sealed record LoginBody(string Token, Guid UserId, string Email, string Username, string DisplayName);
    internal sealed record CreatedBody(Guid Id);
    internal sealed record PlanListBody(Guid Id, string Name, bool IsDefault);
    internal sealed record MoneyBody(decimal Amount, string Currency);
    internal sealed record ItemBody(Guid Id, string Date, string Content, MoneyBody Credit, MoneyBody Debit);
    internal sealed record SummaryBody(List<ItemBody> Items, MoneyBody TotalCredit, MoneyBody TotalDebit, MoneyBody Balance);
}
