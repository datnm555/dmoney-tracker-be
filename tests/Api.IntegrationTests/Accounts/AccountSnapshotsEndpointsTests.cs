using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Api.IntegrationTests.Accounts;

public sealed class AccountSnapshotsEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
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
        Guid Id, Guid AccountId, DateOnly Date, MoneyBody Balance, MoneyBody? StatementAmount,
        DateOnly? DueDate, MoneyBody? InstallmentRemaining, bool StatementPaid, string? Note);
    private sealed record AccountBody(Guid Id, string Name, SnapshotBody? LatestSnapshot);

    private static async Task<Guid> CreateAccountAsync(HttpClient client, string name = "Thẻ test")
    {
        var response = await client.PostAsJsonAsync("/accounts",
            new { name, type = "credit_card", lastDigits = "1234" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    private static async Task<Guid> AddSnapshotAsync(HttpClient client, Guid accountId, object body)
    {
        var response = await client.PostAsJsonAsync($"/accounts/{accountId}/snapshots", body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    private static async Task<SnapshotBody?> LatestAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<AccountBody>>("/accounts"))!.Single().LatestSnapshot;

    private static async Task<List<SnapshotBody>> HistoryAsync(HttpClient client, Guid accountId) =>
        (await client.GetFromJsonAsync<List<SnapshotBody>>($"/accounts/{accountId}/snapshots"))!;

    private static async Task ShouldFailWithAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.ShouldBe(status);
        (await response.Content.ReadAsStringAsync()).ShouldContain(code);
    }

    [Fact]
    public async Task Snapshots_WithoutToken_Returns401()
    {
        (await factory.CreateClient().GetAsync($"/accounts/{Guid.NewGuid()}/snapshots"))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_EmbedsSnapshotAsLatest_WithEveryField()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("snap-all@example.com", "snapall");
        Guid accountId = await CreateAccountAsync(client);

        Guid id = await AddSnapshotAsync(client, accountId, new
        {
            date = "2026-11-10",
            balance = 12000000m,
            statementAmount = 3500000m,
            dueDate = "2026-11-25",
            installmentRemaining = 8000000m,
            statementPaid = true,
            note = "  sau sao kê "
        });

        SnapshotBody latest = (await LatestAsync(client))!;
        latest.Id.ShouldBe(id);
        latest.AccountId.ShouldBe(accountId);
        latest.Date.ShouldBe(new DateOnly(2026, 11, 10));
        latest.Balance.ShouldBe(new MoneyBody(12000000m, "VND"));
        latest.StatementAmount.ShouldBe(new MoneyBody(3500000m, "VND"));
        latest.DueDate.ShouldBe(new DateOnly(2026, 11, 25));
        latest.InstallmentRemaining.ShouldBe(new MoneyBody(8000000m, "VND"));
        latest.StatementPaid.ShouldBeTrue();
        latest.Note.ShouldBe("sau sao kê");
    }

    [Fact]
    public async Task Latest_LaterDateWins_SameDateLaterCreatedWins_OlderDateAddedLaterDoesNot()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("snap-latest@example.com", "snaplatest");
        Guid accountId = await CreateAccountAsync(client);

        await AddSnapshotAsync(client, accountId, new { date = "2026-11-01", balance = 100m });
        Guid later = await AddSnapshotAsync(client, accountId, new { date = "2026-11-05", balance = 200m });
        (await LatestAsync(client))!.Id.ShouldBe(later);

        // Same date: the one created afterwards wins (sequential POSTs get distinct CreatedAt timestamps).
        Guid sameDateNewer = await AddSnapshotAsync(client, accountId, new { date = "2026-11-05", balance = 300m });
        (await LatestAsync(client))!.Id.ShouldBe(sameDateNewer);

        // Older date added last does not win.
        await AddSnapshotAsync(client, accountId, new { date = "2026-10-01", balance = 999m });
        (await LatestAsync(client))!.Id.ShouldBe(sameDateNewer);

        List<SnapshotBody> history = await HistoryAsync(client, accountId);
        history.Select(h => h.Balance.Amount).ShouldBe([300m, 200m, 100m, 999m]);
    }

    [Fact]
    public async Task Put_ChangesEveryField()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("snap-put@example.com", "snapput");
        Guid accountId = await CreateAccountAsync(client);
        Guid id = await AddSnapshotAsync(client, accountId, new { date = "2026-11-01", balance = 100m, note = "old" });

        var put = await client.PutAsJsonAsync($"/accounts/snapshots/{id}", new
        {
            date = "2026-11-12",
            balance = 5000m,
            statementAmount = 700m,
            dueDate = "2026-12-01",
            installmentRemaining = 900m,
            statementPaid = true,
            note = "new"
        });
        put.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        SnapshotBody s = (await HistoryAsync(client, accountId)).Single();
        s.Date.ShouldBe(new DateOnly(2026, 11, 12));
        s.Balance.Amount.ShouldBe(5000m);
        s.StatementAmount!.Amount.ShouldBe(700m);
        s.DueDate.ShouldBe(new DateOnly(2026, 12, 1));
        s.InstallmentRemaining!.Amount.ShouldBe(900m);
        s.StatementPaid.ShouldBeTrue();
        s.Note.ShouldBe("new");

        // Clearing optional fields on update.
        (await client.PutAsJsonAsync($"/accounts/snapshots/{id}", new { date = "2026-11-12", balance = 5000m }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        s = (await HistoryAsync(client, accountId)).Single();
        s.StatementAmount.ShouldBeNull();
        s.DueDate.ShouldBeNull();
        s.InstallmentRemaining.ShouldBeNull();
        s.StatementPaid.ShouldBeFalse();
        s.Note.ShouldBeNull();
    }

    [Fact]
    public async Task Delete_RemovesSnapshot_AndLatestFallsBack()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("snap-del@example.com", "snapdel");
        Guid accountId = await CreateAccountAsync(client);
        Guid older = await AddSnapshotAsync(client, accountId, new { date = "2026-11-01", balance = 100m });
        Guid newer = await AddSnapshotAsync(client, accountId, new { date = "2026-11-02", balance = 200m });

        (await client.DeleteAsync($"/accounts/snapshots/{newer}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await HistoryAsync(client, accountId)).Single().Id.ShouldBe(older);
        (await LatestAsync(client))!.Id.ShouldBe(older);
    }

    [Fact]
    public async Task DeleteAccount_CascadesSnapshots()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("snap-cascade@example.com", "snapcascade");
        Guid accountId = await CreateAccountAsync(client);
        Guid snapshotId = await AddSnapshotAsync(client, accountId, new { date = "2026-11-01", balance = 100m });

        (await client.DeleteAsync($"/accounts/{accountId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await ShouldFailWithAsync(await client.GetAsync($"/accounts/{accountId}/snapshots"),
            HttpStatusCode.NotFound, "Accounts.NotFound");
        await ShouldFailWithAsync(
            await client.PutAsJsonAsync($"/accounts/snapshots/{snapshotId}", new { date = "2026-11-01", balance = 1m }),
            HttpStatusCode.NotFound, "Accounts.SnapshotNotFound");
    }

    [Fact]
    public async Task Validation_RejectsBadValues()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("snap-val@example.com", "snapval");
        Guid accountId = await CreateAccountAsync(client);
        string url = $"/accounts/{accountId}/snapshots";

        await ShouldFailWithAsync(await client.PostAsJsonAsync(url, new { date = "2026-11-01", balance = -1m }),
            HttpStatusCode.BadRequest, "Accounts.NegativeAmount");
        await ShouldFailWithAsync(
            await client.PostAsJsonAsync(url, new { date = "2026-11-01", balance = 1m, statementAmount = -1m }),
            HttpStatusCode.BadRequest, "Accounts.NegativeAmount");
        await ShouldFailWithAsync(
            await client.PostAsJsonAsync(url, new { date = "2026-11-01", balance = 1m, installmentRemaining = -1m }),
            HttpStatusCode.BadRequest, "Accounts.NegativeAmount");
        await ShouldFailWithAsync(
            await client.PostAsJsonAsync(url, new { date = "2026-11-01", balance = 1m, note = new string('x', 501) }),
            HttpStatusCode.BadRequest, "Accounts.NoteTooLong");

        // Update path validates too, and a failed update leaves the row untouched.
        Guid id = await AddSnapshotAsync(client, accountId, new { date = "2026-11-01", balance = 10m });
        await ShouldFailWithAsync(
            await client.PutAsJsonAsync($"/accounts/snapshots/{id}", new { date = "2026-11-02", balance = -5m }),
            HttpStatusCode.BadRequest, "Accounts.NegativeAmount");
        SnapshotBody s = (await HistoryAsync(client, accountId)).Single();
        s.Balance.Amount.ShouldBe(10m);
        s.Date.ShouldBe(new DateOnly(2026, 11, 1));
    }

    [Fact]
    public async Task Post_WhitespaceNote_StoresNull()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("snap-note@example.com", "snapnote");
        Guid accountId = await CreateAccountAsync(client);
        await AddSnapshotAsync(client, accountId, new { date = "2026-11-01", balance = 1m, note = "  " });

        (await LatestAsync(client))!.Note.ShouldBeNull();
    }

    [Fact]
    public async Task OtherUsersAccountAndSnapshot_Return404()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync("snap-owner@example.com", "snapowner");
        HttpClient other = await CreateAuthenticatedClientAsync("snap-other@example.com", "snapother");
        Guid accountId = await CreateAccountAsync(owner);
        Guid snapshotId = await AddSnapshotAsync(owner, accountId, new { date = "2026-11-01", balance = 100m });

        await ShouldFailWithAsync(await other.GetAsync($"/accounts/{accountId}/snapshots"),
            HttpStatusCode.NotFound, "Accounts.NotFound");
        await ShouldFailWithAsync(
            await other.PostAsJsonAsync($"/accounts/{accountId}/snapshots", new { date = "2026-11-01", balance = 1m }),
            HttpStatusCode.NotFound, "Accounts.NotFound");
        await ShouldFailWithAsync(
            await other.PutAsJsonAsync($"/accounts/snapshots/{snapshotId}", new { date = "2026-11-01", balance = 1m }),
            HttpStatusCode.NotFound, "Accounts.SnapshotNotFound");
        await ShouldFailWithAsync(await other.DeleteAsync($"/accounts/snapshots/{snapshotId}"),
            HttpStatusCode.NotFound, "Accounts.SnapshotNotFound");

        (await HistoryAsync(owner, accountId)).Single().Balance.Amount.ShouldBe(100m);
    }
}
