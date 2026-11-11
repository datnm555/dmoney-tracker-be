using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Api.IntegrationTests.Accounts;

public sealed class PayStatementEndpointTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
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
    private sealed record SnapshotBody(
        DateOnly Date, MoneyBody Balance, MoneyBody? StatementAmount, DateOnly? DueDate,
        MoneyBody? InstallmentRemaining, bool StatementPaid, string? Note, bool StatementUntracked);
    private sealed record AccountBody(Guid Id, string Name, SnapshotBody? LatestSnapshot);

    private static async Task<Guid> CreateAccountAsync(HttpClient client, string name, string type)
    {
        var response = await client.PostAsJsonAsync("/accounts", new { name, type });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    private static async Task AddSnapshotAsync(HttpClient client, Guid accountId, object body) =>
        (await client.PostAsJsonAsync($"/accounts/{accountId}/snapshots", body))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

    private static async Task<SnapshotBody?> LatestAsync(HttpClient client, Guid accountId) =>
        (await client.GetFromJsonAsync<List<AccountBody>>("/accounts"))!.Single(a => a.Id == accountId).LatestSnapshot;

    private static Task<HttpResponseMessage> PayAsync(HttpClient client, Guid cardId, decimal amount,
        Guid? fromAccountId = null, string date = "2026-11-11") =>
        client.PostAsJsonAsync($"/accounts/{cardId}/pay-statement", new { date, amount, fromAccountId });

    private static async Task ShouldFailWithAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.ShouldBe(status);
        (await response.Content.ReadAsStringAsync()).ShouldContain(code);
    }

    private static async Task<Guid> CardWithStatementAsync(HttpClient client)
    {
        Guid card = await CreateAccountAsync(client, "VPBank Diamond", "credit_card");
        await AddSnapshotAsync(client, card, new
        {
            date = "2026-11-10",
            balance = 24_625_403m,
            statementAmount = 16_135_984m,
            dueDate = "2026-11-11",
            statementPaid = false,
            note = "from app",
            statementUntracked = true
        });
        return card;
    }

    [Fact]
    public async Task FullPayment_FromOverdraft_LowersCardDebt_RaisesOverdraftDebt()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("pay-od@example.com", "payod");
        Guid card = await CardWithStatementAsync(client);
        Guid overdraft = await CreateAccountAsync(client, "VPBank thấu chi", "overdraft");
        await AddSnapshotAsync(client, overdraft, new { date = "2026-11-10", balance = 210_000_000m, statementPaid = false });

        (await PayAsync(client, card, 16_135_984m, overdraft)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        SnapshotBody c = (await LatestAsync(client, card))!;
        c.Date.ShouldBe(new DateOnly(2026, 11, 11));
        c.Balance.Amount.ShouldBe(8_489_419m);
        c.StatementAmount!.Amount.ShouldBe(16_135_984m);
        c.DueDate.ShouldBe(new DateOnly(2026, 11, 11));
        c.StatementPaid.ShouldBeTrue();
        c.StatementUntracked.ShouldBeTrue();
        c.Note.ShouldBeNull();
        (await LatestAsync(client, overdraft))!.Balance.Amount.ShouldBe(226_135_984m);
    }

    [Fact]
    public async Task PartialPayment_FromChecking_KeepsRemainderOpen_AndSpendsBalance()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("pay-part@example.com", "paypart");
        Guid card = await CardWithStatementAsync(client);
        Guid checking = await CreateAccountAsync(client, "TCB", "checking");
        await AddSnapshotAsync(client, checking, new { date = "2026-11-10", balance = 10_000_000m, statementPaid = false });

        (await PayAsync(client, card, 6_000_000m, checking)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        SnapshotBody c = (await LatestAsync(client, card))!;
        c.Balance.Amount.ShouldBe(18_625_403m);
        c.StatementAmount!.Amount.ShouldBe(10_135_984m);
        c.StatementPaid.ShouldBeFalse();
        (await LatestAsync(client, checking))!.Balance.Amount.ShouldBe(4_000_000m);
    }

    [Fact]
    public async Task Payment_WithoutSource_OnlyChangesCard_AndNeverGoesBelowZero()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("pay-nosrc@example.com", "paynosrc");
        Guid card = await CardWithStatementAsync(client);

        (await PayAsync(client, card, 30_000_000m)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        SnapshotBody c = (await LatestAsync(client, card))!;
        c.Balance.Amount.ShouldBe(0m);
        c.StatementPaid.ShouldBeTrue();
    }

    [Fact]
    public async Task Payment_DatedBeforeLatestReading_StillBecomesCurrent()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("pay-date@example.com", "paydate");
        Guid card = await CardWithStatementAsync(client);

        (await PayAsync(client, card, 1_000_000m, date: "2026-11-01")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        SnapshotBody c = (await LatestAsync(client, card))!;
        c.Date.ShouldBe(new DateOnly(2026, 11, 10));
        c.Balance.Amount.ShouldBe(23_625_403m);
    }

    [Fact]
    public async Task Payment_FromAccountWithoutReading_StartsFromZero()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("pay-fresh@example.com", "payfresh");
        Guid card = await CardWithStatementAsync(client);
        Guid overdraft = await CreateAccountAsync(client, "OD", "overdraft");

        (await PayAsync(client, card, 5_000_000m, overdraft)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await LatestAsync(client, overdraft))!.Balance.Amount.ShouldBe(5_000_000m);
    }

    [Fact]
    public async Task Validation_AndIsolation()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("pay-bad@example.com", "paybad");
        Guid card = await CardWithStatementAsync(client);
        Guid checking = await CreateAccountAsync(client, "TCB", "checking");
        Guid emptyCard = await CreateAccountAsync(client, "Empty", "credit_card");

        await ShouldFailWithAsync(await PayAsync(client, card, 0m), HttpStatusCode.BadRequest, "Accounts.InvalidPaymentAmount");
        await ShouldFailWithAsync(await PayAsync(client, card, -1m), HttpStatusCode.BadRequest, "Accounts.InvalidPaymentAmount");
        await ShouldFailWithAsync(await PayAsync(client, checking, 1m), HttpStatusCode.BadRequest, "Accounts.NotCreditCard");
        await ShouldFailWithAsync(await PayAsync(client, emptyCard, 1m), HttpStatusCode.BadRequest, "Accounts.NoBalance");
        await ShouldFailWithAsync(await PayAsync(client, card, 1m, card), HttpStatusCode.BadRequest, "Accounts.InvalidSourceAccount");
        await ShouldFailWithAsync(await PayAsync(client, card, 1m, Guid.NewGuid()), HttpStatusCode.NotFound, "Accounts.NotFound");

        HttpClient other = await CreateAuthenticatedClientAsync("pay-other@example.com", "payother");
        await ShouldFailWithAsync(await PayAsync(other, card, 1m), HttpStatusCode.NotFound, "Accounts.NotFound");
        Guid otherChecking = await CreateAccountAsync(other, "Other", "checking");
        await ShouldFailWithAsync(await PayAsync(client, card, 1m, otherChecking), HttpStatusCode.NotFound, "Accounts.NotFound");

        // Nothing was written by any failed call.
        (await LatestAsync(client, card))!.Balance.Amount.ShouldBe(24_625_403m);
        (await factory.CreateClient().PostAsJsonAsync($"/accounts/{card}/pay-statement", new { date = "2026-11-11", amount = 1m }))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
