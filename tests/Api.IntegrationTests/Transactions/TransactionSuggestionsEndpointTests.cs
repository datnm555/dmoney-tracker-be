using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Api.IntegrationTests.Transactions;

public sealed class TransactionSuggestionsEndpointTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        string tag = Guid.NewGuid().ToString("N")[..12];
        string email = $"sugg-{tag}@example.com";
        HttpClient client = factory.CreateClient();
        var register = await client.PostAsJsonAsync("/users/register",
            new { email, username = $"sugg{tag}", displayName = "Sugg User", password = "password123" });
        register.StatusCode.ShouldBe(HttpStatusCode.OK);
        var login = await client.PostAsJsonAsync("/users/login",
            new { identifier = email, password = "password123" });
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await login.Content.ReadFromJsonAsync<LoginBody>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    private static async Task<Guid> DefaultPlanAsync(HttpClient client)
    {
        var plans = await (await client.GetAsync("/plans")).Content.ReadFromJsonAsync<List<PlanBody>>();
        return plans!.Single(p => p.IsDefault).Id;
    }

    private static async Task<Guid> CreatePlanAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/plans", new { name });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    // Categories are global; reuse one per client so each test creates at most one.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<HttpClient, Task<Guid>> Categories = new();

    private static Task<Guid> CategoryFor(HttpClient client) => Categories.GetValue(client, async c =>
    {
        string name = $"Sugg {Guid.NewGuid():N}"[..30];
        var response = await c.PostAsJsonAsync("/categories", new { name, icon = "tag" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    });

    private static async Task AddAsync(
        HttpClient client, Guid planId, string date, string content, decimal credit = 0, decimal debit = 10000,
        string paymentMethod = "cash")
    {
        Guid categoryId = await CategoryFor(client);
        var response = await client.PostAsJsonAsync("/transactions", new
        {
            categoryId,
            date,
            content,
            creditAmount = credit,
            debitAmount = debit,
            paymentMethod,
            planId
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    private static async Task<List<SuggestionBody>> QueryAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"/transactions/suggestions?{query}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<SuggestionBody>>())!;
    }

    [Fact]
    public async Task Suggestions_MatchesContainsCaseInsensitiveAcrossPlans()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid plan1 = await DefaultPlanAsync(client);
        Guid plan2 = await CreatePlanAsync(client, "Second");
        await AddAsync(client, plan1, "2026-01-10", "Cà phê sáng");
        await AddAsync(client, plan2, "2026-01-11", "Mua CÀ PHÊ hạt");
        await AddAsync(client, plan1, "2026-01-12", "Tiền nhà");

        var result = await QueryAsync(client, "q=cà phê");

        result.Select(r => r.Content).ShouldBe(["Mua CÀ PHÊ hạt", "Cà phê sáng"]);
    }

    [Fact]
    public async Task Suggestions_PatternCharactersAreLiteral()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid plan = await DefaultPlanAsync(client);
        await AddAsync(client, plan, "2026-01-10", "Giảm 50% phí");
        await AddAsync(client, plan, "2026-01-11", "Giảm 500 phí");

        (await QueryAsync(client, "q=50%25")).Select(r => r.Content).ShouldBe(["Giảm 50% phí"]);
        (await QueryAsync(client, "q=%25%25")).ShouldBeEmpty();
        (await QueryAsync(client, "q=__")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Suggestions_DistinctByContentKeepsLatest()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid plan = await DefaultPlanAsync(client);
        await AddAsync(client, plan, "2026-01-10", "Grab", debit: 30000);
        await AddAsync(client, plan, "2026-02-10", "grab", debit: 45000);
        await AddAsync(client, plan, "2026-01-20", "Grab", debit: 35000);

        var result = await QueryAsync(client, "q=grab");

        result.Count.ShouldBe(1);
        result[0].Content.ShouldBe("grab");
        result[0].Amount.Amount.ShouldBe(45000m);
    }

    [Fact]
    public async Task Suggestions_OrderedByDateDescending()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid plan = await DefaultPlanAsync(client);
        await AddAsync(client, plan, "2026-01-10", "Ăn trưa A");
        await AddAsync(client, plan, "2026-03-10", "Ăn trưa C");
        await AddAsync(client, plan, "2026-02-10", "Ăn trưa B");

        var result = await QueryAsync(client, "q=ăn trưa");

        result.Select(r => r.Content).ShouldBe(["Ăn trưa C", "Ăn trưa B", "Ăn trưa A"]);
    }

    [Fact]
    public async Task Suggestions_LimitDefaultsToEightAndCapsAtTwenty()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid plan = await DefaultPlanAsync(client);
        for (int i = 0; i < 25; i++)
        {
            await AddAsync(client, plan, "2026-01-10", $"Item {i:00}");
        }

        (await QueryAsync(client, "q=item")).Count.ShouldBe(8);
        (await QueryAsync(client, "q=item&limit=3")).Count.ShouldBe(3);
        (await QueryAsync(client, "q=item&limit=50")).Count.ShouldBe(20);
        (await QueryAsync(client, "q=item&limit=0")).Count.ShouldBe(8);
    }

    [Fact]
    public async Task Suggestions_ShortOrBlankQueryReturnsEmpty()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid plan = await DefaultPlanAsync(client);
        await AddAsync(client, plan, "2026-01-10", "Ăn");

        (await QueryAsync(client, "q=ă")).ShouldBeEmpty();
        (await QueryAsync(client, "q=%20a%20")).ShouldBeEmpty();
        (await QueryAsync(client, "")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Suggestions_NeverReturnOtherUsersTransactions()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync();
        HttpClient other = await CreateAuthenticatedClientAsync();
        string unique = $"zzq{Guid.NewGuid():N}"[..12];
        await AddAsync(owner, await DefaultPlanAsync(owner), "2026-01-10", $"{unique} secret");

        (await QueryAsync(other, $"q={unique}")).ShouldBeEmpty();
        (await QueryAsync(owner, $"q={unique}")).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Suggestions_IncomeRowIsTypeInWithCreditAmount()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid plan = await DefaultPlanAsync(client);
        await AddAsync(client, plan, "2026-01-10", "Lương tháng", credit: 20000000, debit: 0, paymentMethod: "transfer");
        await AddAsync(client, plan, "2026-01-11", "Chi tiền ăn", debit: 55000);

        var income = (await QueryAsync(client, "q=lương")).Single();
        income.Type.ShouldBe("in");
        income.Amount.Amount.ShouldBe(20000000m);
        income.PaymentMethod.ShouldBe("transfer");

        var expense = (await QueryAsync(client, "q=tiền ăn")).Single();
        expense.Type.ShouldBe("out");
        expense.Amount.Amount.ShouldBe(55000m);
    }

    [Fact]
    public async Task Suggestions_Unauthenticated_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/transactions/suggestions?q=abc");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    internal sealed record LoginBody(string Token, Guid UserId, string Email, string Username, string DisplayName);
    internal sealed record PlanBody(Guid Id, string Name, bool IsDefault);
    internal sealed record CreatedBody(Guid Id);
    internal sealed record MoneyBody(decimal Amount, string Currency);
    internal sealed record SuggestionBody(
        string Content, string Type, MoneyBody Amount, Guid? CategoryId, Guid? SubCategoryId,
        string PaymentMethod, string? CardType, string? Bank);
}
