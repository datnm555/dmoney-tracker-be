using System.Globalization;
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

    [Fact]
    public async Task CreateWithCardPayment_RoundTripsThroughGet()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("cardpayment@example.com", "cardpayment");

        string today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var response = await client.PostAsJsonAsync("/transactions", new
        {
            date = today,
            content = "Netflix Premium",
            creditAmount = 0m,
            debitAmount = 260000m,
            note = (string?)null,
            category = "entertainment",
            paymentMethod = "card",
            cardType = "visa",
            bank = "Techcombank"
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        string month = DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        SummaryBody? summary = await client.GetFromJsonAsync<SummaryBody>($"/transactions?month={month}");
        ItemBody item = summary!.Items.Single(i => i.Content == "Netflix Premium");
        item.PaymentMethod.ShouldBe("card");
        item.CardType.ShouldBe("visa");
        item.Bank.ShouldBe("Techcombank");
    }

    [Fact]
    public async Task CreateCardWithoutCardType_Returns400()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("cardnotype@example.com", "cardnotype");

        string today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var response = await client.PostAsJsonAsync("/transactions", new
        {
            date = today,
            content = "Netflix Premium",
            creditAmount = 0m,
            debitAmount = 260000m,
            note = (string?)null,
            category = (string?)null,
            paymentMethod = "card"
        });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Transactions.CardTypeRequired");
    }

    [Fact]
    public async Task UpdateWithCardPayment_RoundTripsThroughGet()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("cardupdate@example.com", "cardupdate");

        string today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var createResponse = await client.PostAsJsonAsync("/transactions", new
        {
            date = today,
            content = "Netflix Premium",
            creditAmount = 0m,
            debitAmount = 260000m,
            note = (string?)null,
            category = "entertainment",
            paymentMethod = "card",
            cardType = "visa",
            bank = "Techcombank"
        });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        CreatedBody? created = await createResponse.Content.ReadFromJsonAsync<CreatedBody>();

        var updateResponse = await client.PutAsJsonAsync($"/transactions/{created!.Id}", new
        {
            date = today,
            content = "Netflix Premium 4K",
            creditAmount = 0m,
            debitAmount = 320000m,
            note = (string?)null,
            category = "entertainment",
            paymentMethod = "card",
            cardType = "credit",
            bank = "VPBank"
        });
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        string month = DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        SummaryBody? summary = await client.GetFromJsonAsync<SummaryBody>($"/transactions?month={month}");
        ItemBody item = summary!.Items.Single(i => i.Content == "Netflix Premium 4K");
        item.PaymentMethod.ShouldBe("card");
        item.CardType.ShouldBe("credit");
        item.Bank.ShouldBe("VPBank");
    }

    [Fact]
    public async Task CreateWithoutPaymentMethod_DefaultsToTransfer()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("notransfer@example.com", "notransfer");

        string today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var response = await client.PostAsJsonAsync("/transactions", new
        {
            date = today,
            content = "Lunch",
            creditAmount = 0m,
            debitAmount = 50000m,
            note = (string?)null,
            category = (string?)null
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        string month = DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        SummaryBody? summary = await client.GetFromJsonAsync<SummaryBody>($"/transactions?month={month}");
        summary!.Items.Single(i => i.Content == "Lunch").PaymentMethod.ShouldBe("transfer");
    }

    [Fact]
    public async Task ImportTransactions_SavesSignedRowsWithOtherCategory()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("importer@example.com", "importer1");

        string today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var response = await client.PostAsJsonAsync("/transactions/import", new
        {
            rows = new object[]
            {
                new { date = today, content = "Lương import", amount = 28_000_000m, note = (string?)null },
                new { date = today, content = "Tiền điện import", amount = -1_200_000m, note = "kỳ 07/2026" }
            }
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ImportedBody? imported = await response.Content.ReadFromJsonAsync<ImportedBody>();
        imported!.Imported.ShouldBe(2);

        string month = DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        SummaryBody? summary = await client.GetFromJsonAsync<SummaryBody>($"/transactions?month={month}");
        ItemBody salary = summary!.Items.Single(i => i.Content == "Lương import");
        salary.Credit.Amount.ShouldBe(28_000_000m);
        salary.Debit.Amount.ShouldBe(0m);
        salary.Category.ShouldBe("other");
        ItemBody bill = summary.Items.Single(i => i.Content == "Tiền điện import");
        bill.Credit.Amount.ShouldBe(0m);
        bill.Debit.Amount.ShouldBe(1_200_000m);
        bill.Category.ShouldBe("other");
        bill.Note.ShouldBe("kỳ 07/2026");
    }

    [Fact]
    public async Task ImportTransactions_EmptyRows_Returns400()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("importempty@example.com", "importempty");

        var response = await client.PostAsJsonAsync("/transactions/import", new { rows = Array.Empty<object>() });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Transactions.ImportEmpty");
    }


    [Fact]
    public async Task AdvanceFlag_RoundTripsThroughCreateAndUpdate()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("advance@example.com", "advance1");

        string today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var create = await client.PostAsJsonAsync("/transactions", new
        {
            date = today,
            content = "Tiền xe bus ứng trước",
            creditAmount = 0m,
            debitAmount = 2_000_000m,
            note = (string?)null,
            category = (string?)null,
            isAdvance = true
        });
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        CreatedBody? created = await create.Content.ReadFromJsonAsync<CreatedBody>();

        string month = DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        SummaryBody? summary = await client.GetFromJsonAsync<SummaryBody>($"/transactions?month={month}");
        summary!.Items.Single(i => i.Content == "Tiền xe bus ứng trước").IsAdvance.ShouldBeTrue();

        var update = await client.PutAsJsonAsync($"/transactions/{created!.Id}", new
        {
            date = today,
            content = "Tiền xe bus ứng trước",
            creditAmount = 0m,
            debitAmount = 2_000_000m,
            note = (string?)null,
            category = (string?)null,
            isAdvance = false
        });
        update.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        summary = await client.GetFromJsonAsync<SummaryBody>($"/transactions?month={month}");
        summary!.Items.Single(i => i.Content == "Tiền xe bus ứng trước").IsAdvance.ShouldBeFalse();
    }

    internal sealed record ImportedBody(int Imported);
    internal sealed record LoginBody(string Token, Guid UserId, string Email, string Username, string DisplayName);
    internal sealed record CreatedBody(Guid Id);
    internal sealed record MoneyBody(decimal Amount, string Currency);
    internal sealed record ItemBody(Guid Id, string Date, string Content, MoneyBody Credit, MoneyBody Debit, string? Note, string? Category, string PaymentMethod, string? CardType, string? Bank, bool IsAdvance);
    internal sealed record SummaryBody(List<ItemBody> Items, MoneyBody TotalCredit, MoneyBody TotalDebit, MoneyBody Balance);
}
