using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Api.IntegrationTests.RecurringPayments;

public sealed class RecurringScheduleEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
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
    private sealed record PlanBody(Guid Id, string Name, bool IsDefault);
    private sealed record MoneyBody(decimal Amount, string Currency);
    private sealed record ItemBody(Guid Id, string Name, MoneyBody Amount, int DayOfMonth, bool IsActive);
    private sealed record RecordBody(
        Guid Id, Guid RecurringPaymentId, string Period, MoneyBody Amount, string PaidDate, Guid? TransactionId);
    private sealed record ScheduleRow(ItemBody Item, string DueDate, RecordBody? Record);

    private static async Task<Guid> CreateItemAsync(HttpClient client, string name, int day, bool active = true)
    {
        var response = await client.PostAsJsonAsync("/recurring-payments",
            new { name, amount = 100m, isVariable = false, dayOfMonth = day, isActive = active });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    private static async Task<Guid> CreateTransactionAsync(HttpClient client)
    {
        var cat = await client.PostAsJsonAsync("/categories", new { name = "RS Cat " + Guid.NewGuid(), icon = "tag" });
        Guid categoryId = (await cat.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
        var plans = await client.GetFromJsonAsync<List<PlanBody>>("/plans");
        var response = await client.PostAsJsonAsync("/transactions", new
        {
            date = "2026-11-05",
            content = "rs tx",
            creditAmount = 0m,
            debitAmount = 100m,
            categoryId,
            planId = plans![0].Id
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    private static Task<HttpResponseMessage> PostRecordAsync(
        HttpClient client, Guid itemId, string period = "2026-11", decimal amount = 100m, Guid? transactionId = null) =>
        client.PostAsJsonAsync($"/recurring-payments/{itemId}/records",
            new { period, amount, paidDate = "2026-11-05", transactionId });

    private static async Task<List<ScheduleRow>> ScheduleAsync(HttpClient client, string month) =>
        (await client.GetFromJsonAsync<List<ScheduleRow>>($"/recurring-payments/schedule?month={month}"))!;

    private static async Task<List<RecordBody>> RecordsAsync(HttpClient client, Guid itemId) =>
        (await client.GetFromJsonAsync<List<RecordBody>>($"/recurring-payments/{itemId}/records"))!;

    private static async Task ShouldFailWithAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.ShouldBe(status);
        (await response.Content.ReadAsStringAsync()).ShouldContain(code);
    }

    [Fact]
    public async Task Schedule_ActiveOnly_SortedByDueDateThenName_WithDueDates()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("rs-sort@example.com", "rssort");
        await CreateItemAsync(client, "Zeta", 5);
        await CreateItemAsync(client, "Alpha", 5);
        await CreateItemAsync(client, "Early", 1);
        await CreateItemAsync(client, "Hidden", 2, active: false);

        List<ScheduleRow> rows = await ScheduleAsync(client, "2026-11");

        rows.Select(r => r.Item.Name).ShouldBe(["Early", "Alpha", "Zeta"]);
        rows.Select(r => r.DueDate).ShouldBe(["2026-11-01", "2026-11-05", "2026-11-05"]);
        rows.ShouldAllBe(r => r.Record == null);
    }

    [Fact]
    public async Task Schedule_HidesItemBeforeItsStartPeriod()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("rs-start@example.com", "rsstart");
        await CreateItemAsync(client, "Always", 3);
        var response = await client.PostAsJsonAsync("/recurring-payments",
            new { name = "From Dec", amount = 100m, dayOfMonth = 5, startPeriod = "2026-12" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        (await ScheduleAsync(client, "2026-11")).Select(r => r.Item.Name).ShouldBe(["Always"]);
        (await ScheduleAsync(client, "2026-12")).Select(r => r.Item.Name).ShouldBe(["Always", "From Dec"]);
        (await ScheduleAsync(client, "2027-01")).Select(r => r.Item.Name).ShouldBe(["Always", "From Dec"]);
    }

    [Fact]
    public async Task Schedule_Day31_ClampsToMonthEnd()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("rs-clamp@example.com", "rsclamp");
        await CreateItemAsync(client, "End", 31);

        (await ScheduleAsync(client, "2026-11")).Single().DueDate.ShouldBe("2026-11-30");
        (await ScheduleAsync(client, "2026-02")).Single().DueDate.ShouldBe("2026-02-28");
        (await ScheduleAsync(client, "2028-02")).Single().DueDate.ShouldBe("2028-02-29");
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("abc")]
    [InlineData("2026")]
    [InlineData("")]
    public async Task Schedule_BadMonth_Returns400(string month)
    {
        HttpClient client = await CreateAuthenticatedClientAsync($"rs-bad{month.Length}{month.GetHashCode():x}@example.com", $"rsbad{month.Length}{Math.Abs(month.GetHashCode()):x}");
        await ShouldFailWithAsync(
            await client.GetAsync($"/recurring-payments/schedule?month={month}"),
            HttpStatusCode.BadRequest, "RecurringPayments.InvalidPeriod");
    }

    [Fact]
    public async Task CreateRecord_AppearsInScheduleOnlyForItsPeriod_AndInHistoryNewestFirst()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("rs-rec@example.com", "rsrec");
        Guid id = await CreateItemAsync(client, "Điện", 5);

        var created = await PostRecordAsync(client, id, "2026-10", 90m);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        Guid recordId = (await created.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
        (await PostRecordAsync(client, id, "2026-11", 120m)).StatusCode.ShouldBe(HttpStatusCode.Created);

        ScheduleRow nov = (await ScheduleAsync(client, "2026-11")).Single();
        nov.Record.ShouldNotBeNull();
        nov.Record!.Period.ShouldBe("2026-11");
        nov.Record.Amount.ShouldBe(new MoneyBody(120m, "VND"));
        nov.Record.PaidDate.ShouldBe("2026-11-05");
        (await ScheduleAsync(client, "2026-10")).Single().Record!.Id.ShouldBe(recordId);
        (await ScheduleAsync(client, "2026-12")).Single().Record.ShouldBeNull();

        List<RecordBody> history = await RecordsAsync(client, id);
        history.Select(r => r.Period).ShouldBe(["2026-11", "2026-10"]);
    }

    [Fact]
    public async Task CreateRecord_SamePeriodTwice_Returns409AlreadyPaid()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("rs-dup@example.com", "rsdup");
        Guid id = await CreateItemAsync(client, "Nước", 5);
        (await PostRecordAsync(client, id)).StatusCode.ShouldBe(HttpStatusCode.Created);

        await ShouldFailWithAsync(await PostRecordAsync(client, id), HttpStatusCode.Conflict, "RecurringPayments.AlreadyPaid");
        (await RecordsAsync(client, id)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task CreateRecord_ConcurrentSamePeriod_OneCreatedRestConflict_NeverServerError()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("rs-race@example.com", "rsrace");
        Guid id = await CreateItemAsync(client, "X", 5);

        HttpResponseMessage[] responses = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => PostRecordAsync(client, id)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(7);
        (await RecordsAsync(client, id)).Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("nope")]
    public async Task CreateRecord_BadPeriod_Returns400(string period)
    {
        HttpClient client = await CreateAuthenticatedClientAsync($"rs-bp{period.Length}@example.com", $"rsbp{period.Length}");
        Guid id = await CreateItemAsync(client, "X", 5);
        await ShouldFailWithAsync(await PostRecordAsync(client, id, period),
            HttpStatusCode.BadRequest, "RecurringPayments.InvalidPeriod");
    }

    [Fact]
    public async Task CreateRecord_NegativeAmount_Returns400()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("rs-neg@example.com", "rsneg");
        Guid id = await CreateItemAsync(client, "X", 5);
        await ShouldFailWithAsync(await PostRecordAsync(client, id, amount: -1m),
            HttpStatusCode.BadRequest, "RecurringPayments.NegativeAmount");
    }

    [Fact]
    public async Task CreateRecord_TransactionOfAnotherUserOrMissing_Returns400()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync("rs-tx-a@example.com", "rstxa");
        HttpClient other = await CreateAuthenticatedClientAsync("rs-tx-b@example.com", "rstxb");
        Guid id = await CreateItemAsync(owner, "X", 5);
        Guid foreignTx = await CreateTransactionAsync(other);

        await ShouldFailWithAsync(await PostRecordAsync(owner, id, transactionId: foreignTx),
            HttpStatusCode.BadRequest, "RecurringPayments.InvalidTransaction");
        await ShouldFailWithAsync(await PostRecordAsync(owner, id, transactionId: Guid.NewGuid()),
            HttpStatusCode.BadRequest, "RecurringPayments.InvalidTransaction");
    }

    [Fact]
    public async Task DeleteRecord_UnpaysSchedule_AndKeepsTransaction()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("rs-del@example.com", "rsdel");
        Guid id = await CreateItemAsync(client, "X", 5);
        Guid txId = await CreateTransactionAsync(client);
        var created = await PostRecordAsync(client, id, transactionId: txId);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        Guid recordId = (await created.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
        (await ScheduleAsync(client, "2026-11")).Single().Record!.TransactionId.ShouldBe(txId);

        (await client.DeleteAsync($"/recurring-payments/records/{recordId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await ScheduleAsync(client, "2026-11")).Single().Record.ShouldBeNull();
        (await RecordsAsync(client, id)).ShouldBeEmpty();
        var plans = await client.GetFromJsonAsync<List<PlanBody>>("/plans");
        string txs = await client.GetStringAsync($"/transactions?planId={plans![0].Id}&month=2026-11");
        txs.ShouldContain(txId.ToString());
    }

    [Fact]
    public async Task DeleteLinkedTransaction_KeepsRecordWithNullTransactionId()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("rs-txdel@example.com", "rstxdel");
        Guid id = await CreateItemAsync(client, "X", 5);
        Guid txId = await CreateTransactionAsync(client);
        (await PostRecordAsync(client, id, transactionId: txId)).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await client.DeleteAsync($"/transactions/{txId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        RecordBody record = (await RecordsAsync(client, id)).Single();
        record.TransactionId.ShouldBeNull();
        (await ScheduleAsync(client, "2026-11")).Single().Record.ShouldNotBeNull();
    }

    [Fact]
    public async Task DeleteItem_CascadesRecords()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("rs-casc@example.com", "rscasc");
        Guid id = await CreateItemAsync(client, "X", 5);
        (await PostRecordAsync(client, id)).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await client.DeleteAsync($"/recurring-payments/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await ShouldFailWithAsync(await client.GetAsync($"/recurring-payments/{id}/records"),
            HttpStatusCode.NotFound, "RecurringPayments.NotFound");
        (await ScheduleAsync(client, "2026-11")).ShouldBeEmpty();
    }

    [Fact]
    public async Task OtherUser_Gets404OnEveryRecordRoute_AndScheduleIsIsolated()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync("rs-iso-a@example.com", "rsisoa");
        HttpClient other = await CreateAuthenticatedClientAsync("rs-iso-b@example.com", "rsisob");
        Guid id = await CreateItemAsync(owner, "X", 5);
        var created = await PostRecordAsync(owner, id);
        Guid recordId = (await created.Content.ReadFromJsonAsync<CreatedBody>())!.Id;

        await ShouldFailWithAsync(await other.GetAsync($"/recurring-payments/{id}/records"),
            HttpStatusCode.NotFound, "RecurringPayments.NotFound");
        await ShouldFailWithAsync(await PostRecordAsync(other, id, "2026-12"),
            HttpStatusCode.NotFound, "RecurringPayments.NotFound");
        await ShouldFailWithAsync(await other.DeleteAsync($"/recurring-payments/records/{recordId}"),
            HttpStatusCode.NotFound, "RecurringPayments.RecordNotFound");
        (await ScheduleAsync(other, "2026-11")).ShouldBeEmpty();
        (await RecordsAsync(owner, id)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task RecordRoutes_WithoutToken_Return401()
    {
        HttpClient anon = factory.CreateClient();
        (await anon.GetAsync("/recurring-payments/schedule?month=2026-11")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anon.GetAsync($"/recurring-payments/{Guid.NewGuid()}/records")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
