using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Api.IntegrationTests.Banks;

public sealed class BanksEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
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

    private sealed record LoginBody(string Token);
    internal sealed record CreatedBody(Guid Id);
    internal sealed record BankBody(Guid Id, string Name);
    internal sealed record PlanListBody(Guid Id, string Name, bool IsDefault);

    private static async Task<Guid> CreateBankAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/banks", new { name });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
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

    [Fact]
    public async Task GetBanks_WithoutToken_Returns401()
    {
        (await factory.CreateClient().GetAsync("/banks")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateAndList_Works()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("bank-create@example.com", "bankcreate");

        (await client.PostAsJsonAsync("/banks", new { name = "Techcombank" })).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await client.PostAsJsonAsync("/banks", new { name = "MoMo" })).StatusCode.ShouldBe(HttpStatusCode.Created);
        // Duplicate name (same user) is a conflict.
        (await client.PostAsJsonAsync("/banks", new { name = "Techcombank" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        // Empty name is a validation error.
        (await client.PostAsJsonAsync("/banks", new { name = " " })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var list = await (await client.GetAsync("/banks")).Content.ReadFromJsonAsync<List<BankBody>>();
        list!.Count.ShouldBe(2);
        list.Select(b => b.Name).ShouldBe(new List<string> { "MoMo", "Techcombank" }); // ordered by name
    }

    [Fact]
    public async Task Rename_Works_AndChecksDuplicates()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("bank-rename@example.com", "bankrename");
        Guid momo = await CreateBankAsync(client, "MoMo");
        await CreateBankAsync(client, "VPBank");

        (await client.PutAsJsonAsync($"/banks/{momo}", new { name = "MoMo Ví" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        // Renaming onto an existing name is a conflict.
        (await client.PutAsJsonAsync($"/banks/{momo}", new { name = "VPBank" }))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var list = await (await client.GetAsync("/banks")).Content.ReadFromJsonAsync<List<BankBody>>();
        list!.Select(b => b.Name).ShouldContain("MoMo Ví");
    }

    [Fact]
    public async Task Delete_Works_AndForeignUserGets404()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("bank-del@example.com", "bankdel");
        Guid unused = await CreateBankAsync(client, "Trống");
        (await client.DeleteAsync($"/banks/{unused}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Foreign user's bank is a 404.
        Guid mine = await CreateBankAsync(client, "Của tôi");
        HttpClient other = await CreateAuthenticatedClientAsync("bank-other@example.com", "bankother");
        (await other.DeleteAsync($"/banks/{mine}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_HasNoInUseGuard_TransactionsKeepTheirFreeText()
    {
        // Transactions store bank as free text (no FK), so deleting a catalog
        // entry succeeds even when a transaction carries the same name — the
        // transaction keeps displaying its stored string.
        HttpClient client = await CreateAuthenticatedClientAsync("bank-freetext@example.com", "bankfreetext");
        Guid categoryId = await CreateCategoryAsync(client, "Chi BankFreeText", "tag");
        Guid planId = await GetDefaultPlanIdAsync(client);
        Guid bank = await CreateBankAsync(client, "MoMo");

        var createTx = await client.PostAsJsonAsync("/transactions", new
        {
            date = "2026-09-10", content = "Cà phê", creditAmount = 0m, debitAmount = 50_000m,
            note = (string?)null, categoryId, planId, paymentMethod = "transfer", bank = "MoMo"
        });
        createTx.StatusCode.ShouldBe(HttpStatusCode.Created);

        (await client.DeleteAsync($"/banks/{bank}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
