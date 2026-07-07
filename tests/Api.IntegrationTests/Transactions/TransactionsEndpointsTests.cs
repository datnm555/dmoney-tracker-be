using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Api.IntegrationTests.Transactions;

public sealed class TransactionsEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
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

    private static object ValidPayload(string content = "Lương tháng 7") => new
    {
        date = "2026-07-05",
        content,
        creditAmount = 15_000_000m,
        debitAmount = 0m,
        note = (string?)null,
        category = "salary"
    };

    [Fact]
    public async Task Endpoints_WithoutToken_Return401()
    {
        HttpClient anonymous = factory.CreateClient();

        (await anonymous.GetAsync("/transactions?month=2026-07")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/transactions", ValidPayload())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.DeleteAsync($"/transactions/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FullCrudFlow_CreateGetUpdateDelete()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("crud@example.com", "cruduser");

        // Create
        var create = await client.PostAsJsonAsync("/transactions", ValidPayload());
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<CreatedBody>();
        created.ShouldNotBeNull();
        create.Headers.Location?.ToString().ShouldBe($"/transactions/{created.Id}");

        // Get month summary
        var get = await client.GetAsync("/transactions?month=2026-07");
        get.StatusCode.ShouldBe(HttpStatusCode.OK);
        var summary = await get.Content.ReadFromJsonAsync<SummaryBody>();
        summary.ShouldNotBeNull();
        summary.Items.Count.ShouldBe(1);
        summary.Items[0].Content.ShouldBe("Lương tháng 7");
        summary.Items[0].Category.ShouldBe("salary");
        summary.TotalCredit.Amount.ShouldBe(15_000_000m);
        summary.Balance.Amount.ShouldBe(15_000_000m);
        summary.Balance.Currency.ShouldBe("VND");

        // Update
        var update = await client.PutAsJsonAsync($"/transactions/{created.Id}", new
        {
            date = "2026-07-06",
            content = "Lương + thưởng",
            creditAmount = 16_000_000m,
            debitAmount = 0m,
            note = "đã sửa"
        });
        update.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterUpdate = await client.GetFromJsonAsync<SummaryBody>("/transactions?month=2026-07");
        afterUpdate!.Items[0].Content.ShouldBe("Lương + thưởng");
        afterUpdate.TotalCredit.Amount.ShouldBe(16_000_000m);

        // Delete
        var delete = await client.DeleteAsync($"/transactions/{created.Id}");
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterDelete = await client.GetFromJsonAsync<SummaryBody>("/transactions?month=2026-07");
        afterDelete!.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_WithBothAmountsZero_Returns400()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("zero@example.com", "zerouser");

        var response = await client.PostAsJsonAsync("/transactions", new
        {
            date = "2026-07-05",
            content = "x",
            creditAmount = 0m,
            debitAmount = 0m,
            note = (string?)null
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Transactions.EmptyAmount");
    }

    [Fact]
    public async Task Get_WithInvalidMonth_Returns400()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("badmonth@example.com", "badmonth");

        var response = await client.GetAsync("/transactions?month=garbage");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateAndDelete_OtherUsersRecord_Return404()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync("owner@example.com", "owneruser");
        HttpClient intruder = await CreateAuthenticatedClientAsync("intruder@example.com", "intruder1");

        var create = await owner.PostAsJsonAsync("/transactions", ValidPayload());
        var created = await create.Content.ReadFromJsonAsync<CreatedBody>();

        var update = await intruder.PutAsJsonAsync($"/transactions/{created!.Id}", ValidPayload("hack"));
        update.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var delete = await intruder.DeleteAsync($"/transactions/{created.Id}");
        delete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    internal sealed record LoginBody(string Token, Guid UserId, string Email, string Username, string DisplayName);
    internal sealed record CreatedBody(Guid Id);
    internal sealed record MoneyBody(decimal Amount, string Currency);
    internal sealed record ItemBody(Guid Id, string Date, string Content, MoneyBody Credit, MoneyBody Debit, string? Note, string? Category);
    internal sealed record SummaryBody(List<ItemBody> Items, MoneyBody TotalCredit, MoneyBody TotalDebit, MoneyBody Balance);
}
