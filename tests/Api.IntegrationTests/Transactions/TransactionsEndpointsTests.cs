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

    private static object ValidPayload(Guid planId, string content = "Lương tháng 7", Guid? categoryId = null) => new
    {
        date = "2026-07-05",
        content,
        creditAmount = 15_000_000m,
        debitAmount = 0m,
        note = (string?)null,
        categoryId,
        planId
    };

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

    internal sealed record PlanListBody(Guid Id, string Name, bool IsDefault);

    [Fact]
    public async Task CreateTransaction_UnknownPlan_Returns404()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("plan-tx404@example.com", "plantx404");
        Guid categoryId = await CreateCategoryAsync(client, "Lương P404", "wallet");

        var create = await client.PostAsJsonAsync("/transactions", new
        {
            date = "2026-07-05",
            content = "Lương",
            creditAmount = 1_000_000m,
            debitAmount = 0m,
            note = (string?)null,
            categoryId,
            planId = Guid.NewGuid()
        });
        create.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateTransaction_MovesBetweenPlans()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("plan-move@example.com", "planmove");
        Guid categoryId = await CreateCategoryAsync(client, "Lương Move", "wallet");
        Guid defaultPlan = await GetDefaultPlanIdAsync(client);
        Guid tripPlan = await CreatePlanAsync(client, "Du lịch");

        var create = await client.PostAsJsonAsync("/transactions", new
        {
            date = "2026-07-05",
            content = "Vé máy bay",
            creditAmount = 0m,
            debitAmount = 2_000_000m,
            note = (string?)null,
            categoryId,
            planId = defaultPlan
        });
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        Guid txId = (await create.Content.ReadFromJsonAsync<CreatedBody>())!.Id;

        var update = await client.PutAsJsonAsync($"/transactions/{txId}", new
        {
            date = "2026-07-05",
            content = "Vé máy bay",
            creditAmount = 0m,
            debitAmount = 2_000_000m,
            note = (string?)null,
            categoryId,
            planId = tripPlan
        });
        update.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task MonthlySummary_IsScopedToPlan()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("plan-scope@example.com", "planscope");
        Guid categoryId = await CreateCategoryAsync(client, "Lương Scope", "wallet");
        Guid defaultPlan = await GetDefaultPlanIdAsync(client);
        Guid tripPlan = await CreatePlanAsync(client, "Du lịch Scope");

        foreach (var (plan, content, amount) in new[]
                 { (defaultPlan, "Lương", 15_000_000m), (tripPlan, "Khách sạn", 3_000_000m) })
        {
            var create = await client.PostAsJsonAsync("/transactions", new
            {
                date = "2026-07-05",
                content,
                creditAmount = amount,
                debitAmount = 0m,
                note = (string?)null,
                categoryId,
                planId = plan
            });
            create.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        var summary = await (await client.GetAsync($"/transactions?month=2026-07&planId={tripPlan}"))
            .Content.ReadFromJsonAsync<SummaryBody>();
        summary!.Items.Count.ShouldBe(1);
        summary.Items[0].Content.ShouldBe("Khách sạn");
        summary.TotalCredit.Amount.ShouldBe(3_000_000m);

        // Missing planId is a client error, not "everything".
        (await client.GetAsync("/transactions?month=2026-07")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Endpoints_WithoutToken_Return401()
    {
        HttpClient anonymous = factory.CreateClient();

        (await anonymous.GetAsync($"/transactions?month=2026-07&planId={Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/transactions", ValidPayload(Guid.NewGuid()))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.DeleteAsync($"/transactions/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FullCrudFlow_CreateGetUpdateDelete()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("crud@example.com", "cruduser");
        Guid defaultPlan = await GetDefaultPlanIdAsync(client);

        // Create (with a shared category picked by id)
        Guid salaryId = await CreateCategoryAsync(client, "Lương CRUD", "wallet");
        var create = await client.PostAsJsonAsync("/transactions", ValidPayload(defaultPlan, categoryId: salaryId));
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<CreatedBody>();
        created.ShouldNotBeNull();
        create.Headers.Location?.ToString().ShouldBe($"/transactions/{created.Id}");

        // Get month summary
        var get = await client.GetAsync($"/transactions?month=2026-07&planId={defaultPlan}");
        get.StatusCode.ShouldBe(HttpStatusCode.OK);
        var summary = await get.Content.ReadFromJsonAsync<SummaryBody>();
        summary.ShouldNotBeNull();
        summary.Items.Count.ShouldBe(1);
        summary.Items[0].Content.ShouldBe("Lương tháng 7");
        summary.Items[0].CategoryId.ShouldBe(salaryId);
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
            note = "đã sửa",
            categoryId = salaryId,
            planId = defaultPlan
        });
        update.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterUpdate = await client.GetFromJsonAsync<SummaryBody>($"/transactions?month=2026-07&planId={defaultPlan}");
        afterUpdate!.Items[0].Content.ShouldBe("Lương + thưởng");
        afterUpdate.TotalCredit.Amount.ShouldBe(16_000_000m);

        // Delete
        var delete = await client.DeleteAsync($"/transactions/{created.Id}");
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterDelete = await client.GetFromJsonAsync<SummaryBody>($"/transactions?month=2026-07&planId={defaultPlan}");
        afterDelete!.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_WithBothAmountsZero_Returns400()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("zero@example.com", "zerouser");
        Guid defaultPlan = await GetDefaultPlanIdAsync(client);

        Guid categoryId = await CreateCategoryAsync(client, "Khác zero", "tag");
        var response = await client.PostAsJsonAsync("/transactions", new
        {
            date = "2026-07-05",
            content = "x",
            creditAmount = 0m,
            debitAmount = 0m,
            note = (string?)null,
            categoryId,
            planId = defaultPlan
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Transactions.EmptyAmount");
    }

    [Fact]
    public async Task Get_WithInvalidMonth_Returns400()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("badmonth@example.com", "badmonth");
        Guid defaultPlan = await GetDefaultPlanIdAsync(client);

        var response = await client.GetAsync($"/transactions?month=garbage&planId={defaultPlan}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateAndDelete_OtherUsersRecord_Return404()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync("owner@example.com", "owneruser");
        HttpClient intruder = await CreateAuthenticatedClientAsync("intruder@example.com", "intruder1");
        Guid ownerPlan = await GetDefaultPlanIdAsync(owner);
        Guid intruderPlan = await GetDefaultPlanIdAsync(intruder);

        Guid ownerCategory = await CreateCategoryAsync(owner, "Lương owner", "wallet");
        var create = await owner.PostAsJsonAsync("/transactions", ValidPayload(ownerPlan, categoryId: ownerCategory));
        var created = await create.Content.ReadFromJsonAsync<CreatedBody>();

        var update = await intruder.PutAsJsonAsync($"/transactions/{created!.Id}", ValidPayload(intruderPlan, "hack"));
        update.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var delete = await intruder.DeleteAsync($"/transactions/{created.Id}");
        delete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateWithCardPayment_RoundTripsThroughGet()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("cardpayment@example.com", "cardpayment");
        Guid defaultPlan = await GetDefaultPlanIdAsync(client);
        Guid categoryId = await CreateCategoryAsync(client, "Cat cardpay", "tag");

        string today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var response = await client.PostAsJsonAsync("/transactions", new
        {
            date = today,
            content = "Netflix Premium",
            creditAmount = 0m,
            debitAmount = 260000m,
            note = (string?)null,
            categoryId,
            paymentMethod = "card",
            cardType = "visa",
            bank = "Techcombank",
            planId = defaultPlan
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        string month = DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        SummaryBody? summary = await client.GetFromJsonAsync<SummaryBody>($"/transactions?month={month}&planId={defaultPlan}");
        ItemBody item = summary!.Items.Single(i => i.Content == "Netflix Premium");
        item.PaymentMethod.ShouldBe("card");
        item.CardType.ShouldBe("visa");
        item.Bank.ShouldBe("Techcombank");
    }

    [Fact]
    public async Task CreateCardWithoutCardType_Returns400()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("cardnotype@example.com", "cardnotype");
        Guid defaultPlan = await GetDefaultPlanIdAsync(client);
        Guid categoryId = await CreateCategoryAsync(client, "Cat cardnotype", "tag");

        string today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var response = await client.PostAsJsonAsync("/transactions", new
        {
            date = today,
            content = "Netflix Premium",
            creditAmount = 0m,
            debitAmount = 260000m,
            note = (string?)null,
            categoryId,
            paymentMethod = "card",
            planId = defaultPlan
        });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Transactions.CardTypeRequired");
    }

    [Fact]
    public async Task UpdateWithCardPayment_RoundTripsThroughGet()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("cardupdate@example.com", "cardupdate");
        Guid defaultPlan = await GetDefaultPlanIdAsync(client);
        Guid categoryId = await CreateCategoryAsync(client, "Cat cardupdate", "tag");

        string today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var createResponse = await client.PostAsJsonAsync("/transactions", new
        {
            date = today,
            content = "Netflix Premium",
            creditAmount = 0m,
            debitAmount = 260000m,
            note = (string?)null,
            categoryId,
            paymentMethod = "card",
            cardType = "visa",
            bank = "Techcombank",
            planId = defaultPlan
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
            categoryId,
            paymentMethod = "card",
            cardType = "credit",
            bank = "VPBank",
            planId = defaultPlan
        });
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        string month = DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        SummaryBody? summary = await client.GetFromJsonAsync<SummaryBody>($"/transactions?month={month}&planId={defaultPlan}");
        ItemBody item = summary!.Items.Single(i => i.Content == "Netflix Premium 4K");
        item.PaymentMethod.ShouldBe("card");
        item.CardType.ShouldBe("credit");
        item.Bank.ShouldBe("VPBank");
    }

    [Fact]
    public async Task CreateWithoutPaymentMethod_DefaultsToTransfer()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("notransfer@example.com", "notransfer");
        Guid defaultPlan = await GetDefaultPlanIdAsync(client);
        Guid categoryId = await CreateCategoryAsync(client, "Cat notransfer", "tag");

        string today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var response = await client.PostAsJsonAsync("/transactions", new
        {
            date = today,
            content = "Lunch",
            creditAmount = 0m,
            debitAmount = 50000m,
            note = (string?)null,
            categoryId,
            planId = defaultPlan
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        string month = DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        SummaryBody? summary = await client.GetFromJsonAsync<SummaryBody>($"/transactions?month={month}&planId={defaultPlan}");
        summary!.Items.Single(i => i.Content == "Lunch").PaymentMethod.ShouldBe("transfer");
    }

    [Fact]
    public async Task ImportTransactions_SavesSignedRowsWithOtherCategory()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("importer@example.com", "importer1");
        Guid defaultPlan = await GetDefaultPlanIdAsync(client);

        string today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var response = await client.PostAsJsonAsync("/transactions/import", new
        {
            rows = new object[]
            {
                new { date = today, content = "Lương import", amount = 28_000_000m, note = (string?)null },
                new { date = today, content = "Tiền điện import", amount = -1_200_000m, note = "kỳ 07/2026" }
            },
            planId = defaultPlan
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ImportedBody? imported = await response.Content.ReadFromJsonAsync<ImportedBody>();
        imported!.Imported.ShouldBe(2);

        string month = DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        SummaryBody? summary = await client.GetFromJsonAsync<SummaryBody>($"/transactions?month={month}&planId={defaultPlan}");
        ItemBody salary = summary!.Items.Single(i => i.Content == "Lương import");
        salary.Credit.Amount.ShouldBe(28_000_000m);
        salary.Debit.Amount.ShouldBe(0m);
        // No shared "other"-coded category exists in a fresh test db.
        salary.CategoryId.ShouldBeNull();
        ItemBody bill = summary.Items.Single(i => i.Content == "Tiền điện import");
        bill.Credit.Amount.ShouldBe(0m);
        bill.Debit.Amount.ShouldBe(1_200_000m);
        bill.CategoryId.ShouldBeNull();
        bill.Note.ShouldBe("kỳ 07/2026");
    }

    [Fact]
    public async Task ImportTransactions_EmptyRows_Returns400()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("importempty@example.com", "importempty");
        Guid defaultPlan = await GetDefaultPlanIdAsync(client);

        var response = await client.PostAsJsonAsync("/transactions/import", new { rows = Array.Empty<object>(), planId = defaultPlan });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Transactions.ImportEmpty");
    }


    [Fact]
    public async Task AdvanceFlag_RoundTripsThroughCreateAndUpdate()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("advance@example.com", "advance1");
        Guid defaultPlan = await GetDefaultPlanIdAsync(client);
        Guid categoryId = await CreateCategoryAsync(client, "Cat advance1", "tag");

        string today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var create = await client.PostAsJsonAsync("/transactions", new
        {
            date = today,
            content = "Tiền xe bus ứng trước",
            creditAmount = 0m,
            debitAmount = 2_000_000m,
            note = (string?)null,
            categoryId,
            isAdvance = true,
            planId = defaultPlan
        });
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        CreatedBody? created = await create.Content.ReadFromJsonAsync<CreatedBody>();

        string month = DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        SummaryBody? summary = await client.GetFromJsonAsync<SummaryBody>($"/transactions?month={month}&planId={defaultPlan}");
        summary!.Items.Single(i => i.Content == "Tiền xe bus ứng trước").IsAdvance.ShouldBeTrue();

        var update = await client.PutAsJsonAsync($"/transactions/{created!.Id}", new
        {
            date = today,
            content = "Tiền xe bus ứng trước",
            creditAmount = 0m,
            debitAmount = 2_000_000m,
            note = (string?)null,
            categoryId,
            isAdvance = false,
            planId = defaultPlan
        });
        update.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        summary = await client.GetFromJsonAsync<SummaryBody>($"/transactions?month={month}&planId={defaultPlan}");
        summary!.Items.Single(i => i.Content == "Tiền xe bus ứng trước").IsAdvance.ShouldBeFalse();
    }


    [Fact]
    public async Task AdvanceReimbursement_LinksMultipleAndClosesThem()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("reimburse@example.com", "reimburse");
        Guid defaultPlan = await GetDefaultPlanIdAsync(client);
        Guid categoryId = await CreateCategoryAsync(client, "Cat reimburse", "tag");

        string today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var advanceIds = new List<Guid>();
        foreach ((string content, decimal amount) in new[] { ("Ứng tiền dầu", 4_900_000m), ("Ứng tiền lốp", 10_000_000m) })
        {
            var createAdvance = await client.PostAsJsonAsync("/transactions", new
            {
                date = today,
                content,
                creditAmount = 0m,
                debitAmount = amount,
                note = (string?)null,
                categoryId,
                isAdvance = true,
                planId = defaultPlan
            });
            createAdvance.StatusCode.ShouldBe(HttpStatusCode.Created);
            advanceIds.Add((await createAdvance.Content.ReadFromJsonAsync<CreatedBody>())!.Id);
        }

        List<AdvanceBody>? open = await client.GetFromJsonAsync<List<AdvanceBody>>($"/transactions/advances/open?planId={defaultPlan}");
        open!.Count.ShouldBe(2);

        // One credit settles both advances at once.
        var createReimb = await client.PostAsJsonAsync("/transactions", new
        {
            date = today,
            content = "Anh Huy hoàn tổng",
            creditAmount = 14_900_000m,
            debitAmount = 0m,
            note = (string?)null,
            categoryId,
            advanceTransactionIds = advanceIds,
            planId = defaultPlan
        });
        createReimb.StatusCode.ShouldBe(HttpStatusCode.Created);
        CreatedBody? reimb = await createReimb.Content.ReadFromJsonAsync<CreatedBody>();

        open = await client.GetFromJsonAsync<List<AdvanceBody>>($"/transactions/advances/open?planId={defaultPlan}");
        open!.ShouldBeEmpty();

        // Editing the reimbursement still sees both of its own advances.
        open = await client.GetFromJsonAsync<List<AdvanceBody>>($"/transactions/advances/open?forTransaction={reimb!.Id}&planId={defaultPlan}");
        open!.Count.ShouldBe(2);

        // A second reimbursement against an already-settled advance is rejected.
        var second = await client.PostAsJsonAsync("/transactions", new
        {
            date = today,
            content = "Hoàn lần 2",
            creditAmount = 2_000_000m,
            debitAmount = 0m,
            note = (string?)null,
            categoryId,
            advanceTransactionIds = new[] { advanceIds[0] },
            planId = defaultPlan
        });
        second.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await second.Content.ReadAsStringAsync()).ShouldContain("Transactions.AdvanceAlreadySettled");

        string month = DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        SummaryBody? summary = await client.GetFromJsonAsync<SummaryBody>($"/transactions?month={month}&planId={defaultPlan}");
        List<Guid> linked = summary!.Items.Single(i => i.Content == "Anh Huy hoàn tổng").AdvanceTransactionIds;
        linked.Count.ShouldBe(2);
        linked.ShouldContain(advanceIds[0]);
        linked.ShouldContain(advanceIds[1]);
    }

    internal sealed record AdvanceBody(Guid Id, string Date, string Content, MoneyBody Debit);

    [Fact]
    public async Task PrepaidCredit_CoversMultipleZeroAmountDebits()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("prepaid@example.com", "prepaid1");
        Guid defaultPlan = await GetDefaultPlanIdAsync(client);
        Guid categoryId = await CreateCategoryAsync(client, "Cat prepaid1", "tag");

        string today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var createPrepaid = await client.PostAsJsonAsync("/transactions", new
        {
            date = today,
            content = "Sinh hoạt 5 tháng",
            creditAmount = 25_000_000m,
            debitAmount = 0m,
            note = (string?)null,
            categoryId,
            isPrepaid = true,
            prepaidFrom = "2026-01-01",
            prepaidTo = "2026-05-31",
            planId = defaultPlan
        });
        createPrepaid.StatusCode.ShouldBe(HttpStatusCode.Created);
        CreatedBody? prepaid = await createPrepaid.Content.ReadFromJsonAsync<CreatedBody>();

        List<PrepaidBody> credits =
            (await client.GetFromJsonAsync<List<PrepaidBody>>($"/transactions/prepaid?planId={defaultPlan}"))!;
        credits.Single().Id.ShouldBe(prepaid!.Id);
        credits[0].PrepaidFrom.ShouldBe("2026-01-01");

        // Two months consume the same prepaid credit — both without an amount.
        foreach (string content in new[] { "Sinh hoạt tháng 2", "Sinh hoạt tháng 3" })
        {
            var linked = await client.PostAsJsonAsync("/transactions", new
            {
                date = today,
                content,
                creditAmount = 0m,
                debitAmount = 0m,
                note = (string?)null,
                categoryId,
                prepaidTransactionId = prepaid.Id,
                planId = defaultPlan
            });
            linked.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        // The prepaid credit stays available for the remaining months.
        credits = (await client.GetFromJsonAsync<List<PrepaidBody>>($"/transactions/prepaid?planId={defaultPlan}"))!;
        credits.Count.ShouldBe(1);

        string month = DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        SummaryBody? summary = await client.GetFromJsonAsync<SummaryBody>($"/transactions?month={month}&planId={defaultPlan}");
        summary!.Items.Count(i => i.PrepaidTransactionId == prepaid.Id).ShouldBe(2);
        summary.Items.Single(i => i.Content == "Sinh hoạt 5 tháng").IsPrepaid.ShouldBeTrue();
    }

    internal sealed record PrepaidBody(Guid Id, string Date, string Content, MoneyBody Credit, string? PrepaidFrom, string? PrepaidTo);


    internal sealed record ImportedBody(int Imported);
    internal sealed record LoginBody(string Token, Guid UserId, string Email, string Username, string DisplayName);
    internal sealed record CreatedBody(Guid Id);
    internal sealed record MoneyBody(decimal Amount, string Currency);
    internal sealed record ItemBody(Guid Id, string Date, string Content, MoneyBody Credit, MoneyBody Debit, string? Note, Guid? CategoryId, string PaymentMethod, string? CardType, string? Bank, bool IsAdvance, List<Guid> AdvanceTransactionIds, bool IsPrepaid, string? PrepaidFrom, string? PrepaidTo, Guid? PrepaidTransactionId);
    internal sealed record SummaryBody(List<ItemBody> Items, MoneyBody TotalCredit, MoneyBody TotalDebit, MoneyBody Balance);
}
