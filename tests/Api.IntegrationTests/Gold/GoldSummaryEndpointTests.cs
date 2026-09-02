using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Api.IntegrationTests.Gold;

public sealed class GoldSummaryEndpointTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
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

    private static async Task<Guid> CreatePlanAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/plans", new { name });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    private static async Task<Guid> CreateGoldTypeAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/gold-types", new { name });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    internal sealed record LoginBody(string Token, Guid UserId, string Email, string Username, string DisplayName);
    internal sealed record CreatedBody(Guid Id);
    internal sealed record PlanListBody(Guid Id, string Name, bool IsDefault);
    internal sealed record MoneyBody(decimal Amount, string Currency);

    internal sealed record GoldSummaryBody(List<GoldSummaryBody.TypeItem> Types, List<GoldSummaryBody.TxItem> Transactions)
    {
        internal sealed record TypeItem(
            Guid GoldTypeId, string Name, decimal HeldQuantity, decimal BoughtQuantity, decimal SoldQuantity,
            MoneyBody TotalSpent, MoneyBody TotalReceived, MoneyBody AverageCostPerChi);

        internal sealed record TxItem(
            Guid TransactionId, string Date, string Content, Guid GoldTypeId, string GoldTypeName,
            decimal GoldQuantity, MoneyBody Credit, MoneyBody Debit, MoneyBody PricePerChi);
    }

    [Fact]
    public async Task GoldSummary_WithoutToken_Returns401()
    {
        (await factory.CreateClient().GetAsync("/gold/summary")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GoldSummary_AggregatesAcrossPlans()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("gold-sum@example.com", "goldsum");
        Guid categoryId = await CreateCategoryAsync(client, "Chi GSum", "tag");
        Guid defaultPlanId = await GetDefaultPlanIdAsync(client);
        Guid otherPlanId = await CreatePlanAsync(client, "Sổ vàng riêng");
        Guid ring = await CreateGoldTypeAsync(client, "Nhẫn trơn");
        await CreateGoldTypeAsync(client, "SJC"); // never traded — must still appear with zeros

        async Task PostTx(Guid planId, string date, string content, decimal credit, decimal debit, decimal qty) =>
            (await client.PostAsJsonAsync("/transactions", new
            {
                date, content, creditAmount = credit, debitAmount = debit,
                note = (string?)null, categoryId, planId, goldTypeId = ring, goldQuantity = qty
            })).StatusCode.ShouldBe(HttpStatusCode.Created);

        await PostTx(defaultPlanId, "2026-08-01", "Mua 2 chỉ", 0m, 20_000_000m, 2m);
        await PostTx(otherPlanId, "2026-08-02", "Mua 1 chỉ", 0m, 11_000_000m, 1m);   // other plan — must count
        await PostTx(defaultPlanId, "2026-08-03", "Bán 1 chỉ", 12_000_000m, 0m, 1m);

        var body = await (await client.GetAsync("/gold/summary")).Content.ReadFromJsonAsync<GoldSummaryBody>();

        body!.Types.Count.ShouldBe(2); // name order: "Nhẫn trơn", "SJC"
        GoldSummaryBody.TypeItem nhan = body.Types.Single(x => x.Name == "Nhẫn trơn");
        nhan.BoughtQuantity.ShouldBe(3m);
        nhan.SoldQuantity.ShouldBe(1m);
        nhan.HeldQuantity.ShouldBe(2m);
        nhan.TotalSpent.Amount.ShouldBe(31_000_000m);
        nhan.TotalReceived.Amount.ShouldBe(12_000_000m);
        nhan.AverageCostPerChi.Amount.ShouldBe(Math.Round(31_000_000m / 3m, 2));

        GoldSummaryBody.TypeItem sjc = body.Types.Single(x => x.Name == "SJC");
        sjc.HeldQuantity.ShouldBe(0m);
        sjc.AverageCostPerChi.Amount.ShouldBe(0m);

        body.Transactions.Count.ShouldBe(3); // date desc
        body.Transactions[0].Content.ShouldBe("Bán 1 chỉ");
        body.Transactions[0].PricePerChi.Amount.ShouldBe(12_000_000m);
        body.Transactions.Single(x => x.Content == "Mua 2 chỉ").PricePerChi.Amount.ShouldBe(10_000_000m);
    }
}
