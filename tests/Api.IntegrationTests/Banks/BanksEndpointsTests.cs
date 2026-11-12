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
    internal sealed record BankBody(Guid Id, string Name, bool IsDefault);
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
        list.Select(b => b.Name).ShouldBe(new List<string> { "Techcombank", "MoMo" }); // creation order until reordered
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
    public async Task Rename_FollowsToAccountsTransactionsAndRecurringItems_OfThatUserOnly()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("bank-cascade@example.com", "bankcascade");
        HttpClient other = await CreateAuthenticatedClientAsync("bank-cascade2@example.com", "bankcascade2");
        Guid vpb = await CreateBankAsync(client, "VPB");
        Guid categoryId = await CreateCategoryAsync(client, $"Cascade {Guid.NewGuid():N}"[..20], "tag");
        Guid planId = await GetDefaultPlanIdAsync(client);

        var account = await client.PostAsJsonAsync("/accounts", new { name = "Lương", type = "checking", bankName = "VPB" });
        account.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await client.PostAsJsonAsync("/transactions", new
        {
            date = "2026-11-12", content = "Grab", creditAmount = 0m, debitAmount = 50_000m,
            categoryId, planId, paymentMethod = "transfer", bank = "VPB"
        })).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await client.PostAsJsonAsync("/recurring-payments", new
        {
            name = "Tiền nhà", amount = 1m, isVariable = false, dayOfMonth = 5, paymentMethod = "transfer", bank = "VPB"
        })).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await other.PostAsJsonAsync("/accounts", new { name = "Khác", type = "checking", bankName = "VPB" }))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        (await client.PutAsJsonAsync($"/banks/{vpb}", new { name = "VPBank" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.GetFromJsonAsync<List<NamedBankBody>>("/accounts"))!.Single().BankName.ShouldBe("VPBank");
        var summary = await client.GetFromJsonAsync<TxSummaryBody>($"/transactions?month=2026-11&planId={planId}");
        summary!.Items.Single().Bank.ShouldBe("VPBank");
        (await client.GetFromJsonAsync<List<RecurringBankBody>>("/recurring-payments"))!.Single().Bank.ShouldBe("VPBank");
        (await other.GetFromJsonAsync<List<NamedBankBody>>("/accounts"))!.Single().BankName.ShouldBe("VPB");
    }

    internal sealed record NamedBankBody(Guid Id, string? BankName);
    internal sealed record TxItemBody(Guid Id, string? Bank);
    internal sealed record TxSummaryBody(List<TxItemBody> Items);
    internal sealed record RecurringBankBody(Guid Id, string? Bank);

    [Fact]
    public async Task SetDefault_Switches_WithoutDuplicating()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("bank-default@example.com", "bankdefault");
        Guid momo = await CreateBankAsync(client, "MoMo");
        Guid vpb = await CreateBankAsync(client, "VPBank");

        // No default until the user sets one.
        var list = await (await client.GetAsync("/banks")).Content.ReadFromJsonAsync<List<BankBody>>();
        list!.All(b => !b.IsDefault).ShouldBeTrue();

        (await client.PutAsync($"/banks/{vpb}/default", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        list = await (await client.GetAsync("/banks")).Content.ReadFromJsonAsync<List<BankBody>>();
        list!.Single(b => b.IsDefault).Id.ShouldBe(vpb);

        // Switching moves the flag, never duplicates it.
        (await client.PutAsync($"/banks/{momo}/default", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        list = await (await client.GetAsync("/banks")).Content.ReadFromJsonAsync<List<BankBody>>();
        list!.Single(b => b.IsDefault).Id.ShouldBe(momo);

        // Foreign user's bank is a 404.
        HttpClient other = await CreateAuthenticatedClientAsync("bank-default2@example.com", "bankdefault2");
        (await other.PutAsync($"/banks/{momo}/default", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Order_NewBanksAppend_ReorderRewritesIt_AndSurvivesDefault()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("bank-order@example.com", "bankorder");
        Guid acb = await CreateBankAsync(client, "ACB");
        Guid tcb = await CreateBankAsync(client, "Techcombank");
        Guid vpb = await CreateBankAsync(client, "VPBank");

        async Task<List<Guid>> OrderAsync() =>
            (await (await client.GetAsync("/banks")).Content.ReadFromJsonAsync<List<BankBody>>())!
                .Select(b => b.Id).ToList();

        (await OrderAsync()).ShouldBe([acb, tcb, vpb]); // creation order

        (await client.PutAsJsonAsync("/banks/order", new { ids = new[] { vpb, tcb, acb } }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await OrderAsync()).ShouldBe([vpb, tcb, acb]);

        // Setting a default no longer reorders.
        (await client.PutAsync($"/banks/{acb}/default", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await OrderAsync()).ShouldBe([vpb, tcb, acb]);

        Guid momo = await CreateBankAsync(client, "MoMo");
        (await OrderAsync()).ShouldBe([vpb, tcb, acb, momo]); // appended last
    }

    [Fact]
    public async Task Order_MustListEachOwnBankOnce()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("bank-order-bad@example.com", "bankorderbad");
        Guid a = await CreateBankAsync(client, "A");
        Guid b = await CreateBankAsync(client, "B");
        HttpClient other = await CreateAuthenticatedClientAsync("bank-order-bad2@example.com", "bankorderbad2");
        Guid foreign = await CreateBankAsync(other, "X");

        foreach (Guid[] ids in new[] { new[] { a }, new[] { a, a }, new[] { a, foreign }, new[] { a, b, foreign } })
        {
            HttpResponseMessage response = await client.PutAsJsonAsync("/banks/order", new { ids });
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).ShouldContain("Banks.InvalidOrder");
        }
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
