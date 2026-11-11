using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Api.IntegrationTests.Accounts;

public sealed class AccountsEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
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
    private sealed record CreatedBody(Guid Id);
    private sealed record MoneyBody(decimal Amount, string Currency);
    private sealed record AccountBody(
        Guid Id, string Name, string Type, string? LastDigits, MoneyBody? CreditLimit,
        int? StatementDay, int? DueDay, decimal? InterestRatePercent, JsonElement? LatestSnapshot,
        string? BankName = null, bool IsDefault = false);

    private static async Task<Guid> CreateAsync(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/accounts", body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    private static async Task<List<AccountBody>> ListAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<AccountBody>>("/accounts"))!;

    private static async Task ShouldFailWithAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.ShouldBe(status);
        (await response.Content.ReadAsStringAsync()).ShouldContain(code);
    }

    [Fact]
    public async Task GetAccounts_WithoutToken_Returns401()
    {
        (await factory.CreateClient().GetAsync("/accounts")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_ThenList_ReturnsEveryField()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("acc-create@example.com", "acccreate");
        Guid id = await CreateAsync(client, new
        {
            name = "  VPBank thẻ ",
            type = "credit_card",
            lastDigits = "1234",
            creditLimit = 50000000m,
            statementDay = 5,
            dueDay = 25,
            interestRatePercent = 30.5m
        });

        AccountBody account = (await ListAsync(client)).Single();
        account.Id.ShouldBe(id);
        account.Name.ShouldBe("VPBank thẻ");
        account.Type.ShouldBe("credit_card");
        account.LastDigits.ShouldBe("1234");
        account.CreditLimit.ShouldBe(new MoneyBody(50000000m, "VND"));
        account.StatementDay.ShouldBe(5);
        account.DueDay.ShouldBe(25);
        account.InterestRatePercent.ShouldBe(30.5m);
        account.LatestSnapshot.ShouldBeNull();
    }

    [Fact]
    public async Task Create_BlankLastDigits_StoresNull()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("acc-blank@example.com", "accblank");
        await CreateAsync(client, new { name = "Ví", type = "ewallet", lastDigits = "  " });

        AccountBody account = (await ListAsync(client)).Single();
        account.LastDigits.ShouldBeNull();
        account.CreditLimit.ShouldBeNull();
    }

    [Fact]
    public async Task Update_ChangesEveryField()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("acc-update@example.com", "accupdate");
        Guid id = await CreateAsync(client, new { name = "Old", type = "checking" });

        var put = await client.PutAsJsonAsync($"/accounts/{id}", new
        {
            name = "New name",
            type = "overdraft",
            lastDigits = "9876",
            creditLimit = 250000000m,
            statementDay = 1,
            dueDay = 31,
            interestRatePercent = 15m
        });
        put.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        AccountBody account = (await ListAsync(client)).Single();
        account.Name.ShouldBe("New name");
        account.Type.ShouldBe("overdraft");
        account.LastDigits.ShouldBe("9876");
        account.CreditLimit!.Amount.ShouldBe(250000000m);
        account.StatementDay.ShouldBe(1);
        account.DueDay.ShouldBe(31);
        account.InterestRatePercent.ShouldBe(15m);

        // Clearing the optional fields on update.
        (await client.PutAsJsonAsync($"/accounts/{id}", new { name = "New name", type = "overdraft" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        account = (await ListAsync(client)).Single();
        account.LastDigits.ShouldBeNull();
        account.CreditLimit.ShouldBeNull();
        account.StatementDay.ShouldBeNull();
        account.DueDay.ShouldBeNull();
        account.InterestRatePercent.ShouldBeNull();
    }

    [Fact]
    public async Task BankName_CreateUpdateClear_RoundTrips()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("acc-bank@example.com", "accbank");
        Guid id = await CreateAsync(client, new { name = "Lương", type = "checking", bankName = "  Techcombank " });
        (await ListAsync(client)).Single().BankName.ShouldBe("Techcombank");

        (await client.PutAsJsonAsync($"/accounts/{id}", new { name = "Lương", type = "checking", bankName = "VPBank" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ListAsync(client)).Single().BankName.ShouldBe("VPBank");

        (await client.PutAsJsonAsync($"/accounts/{id}", new { name = "Lương", type = "checking", bankName = (string?)null }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ListAsync(client)).Single().BankName.ShouldBeNull();
    }

    [Fact]
    public async Task BankName_Whitespace_StoresNull()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("acc-bankws@example.com", "accbankws");
        await CreateAsync(client, new { name = "Ví", type = "ewallet", bankName = "   " });
        (await ListAsync(client)).Single().BankName.ShouldBeNull();
    }

    [Fact]
    public async Task Delete_RemovesAccount()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("acc-delete@example.com", "accdelete");
        Guid id = await CreateAsync(client, new { name = "Tiền mặt", type = "cash" });

        (await client.DeleteAsync($"/accounts/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ListAsync(client)).ShouldBeEmpty();
        await ShouldFailWithAsync(await client.DeleteAsync($"/accounts/{id}"), HttpStatusCode.NotFound, "Accounts.NotFound");
    }

    [Fact]
    public async Task List_OrdersByTypeGroupThenName()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("acc-order@example.com", "accorder");
        await CreateAsync(client, new { name = "B card", type = "credit_card" });
        await CreateAsync(client, new { name = "A card", type = "credit_card" });
        await CreateAsync(client, new { name = "Overdraft", type = "overdraft" });
        await CreateAsync(client, new { name = "Momo", type = "ewallet" });
        await CreateAsync(client, new { name = "Zeta", type = "checking" });
        await CreateAsync(client, new { name = "Alpha", type = "checking" });
        await CreateAsync(client, new { name = "Cash", type = "cash" });

        (await ListAsync(client)).Select(a => a.Name).ShouldBe(
            new List<string> { "Alpha", "Zeta", "Momo", "Cash", "Overdraft", "A card", "B card" });
    }

    public static TheoryData<object, string> InvalidBodies => new()
    {
        { new { name = " ", type = "cash" }, "Accounts.NameRequired" },
        { new { name = new string('x', 101), type = "cash" }, "Accounts.NameTooLong" },
        { new { name = "A", type = "savings" }, "Accounts.InvalidType" },
        { new { name = "A", type = "cash", lastDigits = "12a4" }, "Accounts.InvalidLastDigits" },
        { new { name = "A", type = "cash", lastDigits = "123" }, "Accounts.InvalidLastDigits" },
        { new { name = "A", type = "cash", statementDay = 0 }, "Accounts.InvalidDay" },
        { new { name = "A", type = "cash", dueDay = 32 }, "Accounts.InvalidDay" },
        { new { name = "A", type = "cash", creditLimit = -1m }, "Accounts.NegativeAmount" },
        { new { name = "A", type = "cash", interestRatePercent = -1m }, "Accounts.InvalidInterestRate" },
        { new { name = "A", type = "cash", interestRatePercent = 101m }, "Accounts.InvalidInterestRate" },
        { new { name = "A", type = "cash", bankName = new string('x', 101) }, "Accounts.BankNameTooLong" }
    };

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task Create_Invalid_Returns400(object body, string code)
    {
        HttpClient client = await CreateAuthenticatedClientAsync($"acc-v-{Guid.NewGuid():N}@example.com", $"accv{Guid.NewGuid():N}"[..20]);
        await ShouldFailWithAsync(await client.PostAsJsonAsync("/accounts", body), HttpStatusCode.BadRequest, code);
    }

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task Update_Invalid_Returns400(object body, string code)
    {
        HttpClient client = await CreateAuthenticatedClientAsync($"acc-vu-{Guid.NewGuid():N}@example.com", $"accu{Guid.NewGuid():N}"[..20]);
        Guid id = await CreateAsync(client, new { name = "Ok", type = "cash" });
        await ShouldFailWithAsync(await client.PutAsJsonAsync($"/accounts/{id}", body), HttpStatusCode.BadRequest, code);
    }

    [Fact]
    public async Task OtherUsersAccount_IsInvisible_AndReturns404()
    {
        HttpClient userA = await CreateAuthenticatedClientAsync("acc-a@example.com", "accusera");
        HttpClient userB = await CreateAuthenticatedClientAsync("acc-b@example.com", "accuserb");
        Guid id = await CreateAsync(userA, new { name = "Secret", type = "checking" });

        (await ListAsync(userB)).ShouldBeEmpty();
        await ShouldFailWithAsync(
            await userB.PutAsJsonAsync($"/accounts/{id}", new { name = "Hacked", type = "cash" }),
            HttpStatusCode.NotFound, "Accounts.NotFound");
        await ShouldFailWithAsync(await userB.DeleteAsync($"/accounts/{id}"), HttpStatusCode.NotFound, "Accounts.NotFound");
        await ShouldFailWithAsync(
            await userB.PutAsync($"/accounts/{id}/default", null), HttpStatusCode.NotFound, "Accounts.NotFound");
        await ShouldFailWithAsync(
            await userB.DeleteAsync($"/accounts/{id}/default"), HttpStatusCode.NotFound, "Accounts.NotFound");
        (await ListAsync(userA)).Single().Name.ShouldBe("Secret");
    }

    [Fact]
    public async Task Default_SetSwitchesAndClears()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("acc-default@example.com", "accdefault");
        Guid tcb = await CreateAsync(client, new { name = "TCB", type = "checking" });
        Guid momo = await CreateAsync(client, new { name = "Momo", type = "ewallet" });

        // No default until the user picks one.
        (await ListAsync(client)).ShouldAllBe(a => !a.IsDefault);

        (await client.PutAsync($"/accounts/{tcb}/default", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ListAsync(client)).Single(a => a.IsDefault).Id.ShouldBe(tcb);

        // Picking another moves the flag; there is never more than one.
        (await client.PutAsync($"/accounts/{momo}/default", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ListAsync(client)).Single(a => a.IsDefault).Id.ShouldBe(momo);

        (await client.DeleteAsync($"/accounts/{momo}/default")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ListAsync(client)).ShouldAllBe(a => !a.IsDefault);
    }
}
