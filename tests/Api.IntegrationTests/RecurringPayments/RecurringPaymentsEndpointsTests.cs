using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Api.IntegrationTests.RecurringPayments;

public sealed class RecurringPaymentsEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
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
    private sealed record ItemBody(
        Guid Id, string Name, Guid? CategoryId, Guid? SubCategoryId, MoneyBody Amount,
        bool IsVariable, int DayOfMonth, string? PaymentMethod, string? Bank, string? Note, bool IsActive);

    private static async Task<Guid> CreateCategoryAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/categories", new { name, icon = "tag" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    private static async Task<Guid> CreateSubCategoryAsync(HttpClient client, Guid categoryId, string name)
    {
        var response = await client.PostAsJsonAsync("/subcategories", new { categoryId, name });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    private static async Task<Guid> CreateAsync(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/recurring-payments", body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    private static async Task<List<ItemBody>> ListAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<ItemBody>>("/recurring-payments"))!;

    private static async Task ShouldFailWithAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.ShouldBe(status);
        (await response.Content.ReadAsStringAsync()).ShouldContain(code);
    }

    private static object Valid(string name = "Tiền điện") =>
        new { name, amount = 100m, isVariable = false, dayOfMonth = 5 };

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        (await factory.CreateClient().GetAsync("/recurring-payments")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_ThenList_ReturnsEveryField()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("rp-create@example.com", "rpcreate");
        Guid categoryId = await CreateCategoryAsync(client, "RP Cat create");
        Guid subId = await CreateSubCategoryAsync(client, categoryId, "RP Sub create");
        Guid id = await CreateAsync(client, new
        {
            name = "  Tiền điện ",
            categoryId,
            subCategoryId = subId,
            amount = 1200000.5m,
            isVariable = true,
            dayOfMonth = 31,
            paymentMethod = "transfer",
            bank = " Techcombank ",
            note = " ghi chú ",
            isActive = true
        });

        ItemBody item = (await ListAsync(client)).Single();
        item.Id.ShouldBe(id);
        item.Name.ShouldBe("Tiền điện");
        item.CategoryId.ShouldBe(categoryId);
        item.SubCategoryId.ShouldBe(subId);
        item.Amount.ShouldBe(new MoneyBody(1200000.5m, "VND"));
        item.IsVariable.ShouldBeTrue();
        item.DayOfMonth.ShouldBe(31);
        item.PaymentMethod.ShouldBe("transfer");
        item.Bank.ShouldBe("Techcombank");
        item.Note.ShouldBe("ghi chú");
        item.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Create_Defaults_ActiveAndOptionalsNull_BlankBankNoteBecomeNull()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("rp-blank@example.com", "rpblank");
        await CreateAsync(client, new { name = "Gửi xe", amount = 0m, dayOfMonth = 1, bank = "  ", note = " " });

        ItemBody item = (await ListAsync(client)).Single();
        item.IsActive.ShouldBeTrue();
        item.IsVariable.ShouldBeFalse();
        item.Bank.ShouldBeNull();
        item.Note.ShouldBeNull();
        item.PaymentMethod.ShouldBeNull();
        item.CategoryId.ShouldBeNull();
        item.SubCategoryId.ShouldBeNull();
    }

    [Fact]
    public async Task Update_ChangesEveryField_ThenClearsOptionals()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("rp-update@example.com", "rpupdate");
        Guid categoryId = await CreateCategoryAsync(client, "RP Cat update");
        Guid subId = await CreateSubCategoryAsync(client, categoryId, "RP Sub update");
        Guid id = await CreateAsync(client, new { name = "Old", amount = 1m, dayOfMonth = 2 });

        var put = await client.PutAsJsonAsync($"/recurring-payments/{id}", new
        {
            name = "New name",
            categoryId,
            subCategoryId = subId,
            amount = 555.25m,
            isVariable = true,
            dayOfMonth = 28,
            paymentMethod = "cash",
            bank = "VPBank",
            note = "n",
            isActive = false
        });
        put.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        ItemBody item = (await ListAsync(client)).Single();
        item.Name.ShouldBe("New name");
        item.CategoryId.ShouldBe(categoryId);
        item.SubCategoryId.ShouldBe(subId);
        item.Amount.Amount.ShouldBe(555.25m);
        item.IsVariable.ShouldBeTrue();
        item.DayOfMonth.ShouldBe(28);
        item.PaymentMethod.ShouldBe("cash");
        item.Bank.ShouldBe("VPBank");
        item.Note.ShouldBe("n");
        item.IsActive.ShouldBeFalse();

        (await client.PutAsJsonAsync($"/recurring-payments/{id}",
                new { name = "New name", amount = 0m, dayOfMonth = 28, isActive = true }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        item = (await ListAsync(client)).Single();
        item.CategoryId.ShouldBeNull();
        item.SubCategoryId.ShouldBeNull();
        item.PaymentMethod.ShouldBeNull();
        item.Bank.ShouldBeNull();
        item.Note.ShouldBeNull();
        item.IsVariable.ShouldBeFalse();
        item.IsActive.ShouldBeTrue();
        item.Amount.Amount.ShouldBe(0m);
    }

    [Fact]
    public async Task Delete_RemovesItem()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("rp-delete@example.com", "rpdelete");
        Guid id = await CreateAsync(client, Valid());

        (await client.DeleteAsync($"/recurring-payments/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ListAsync(client)).ShouldBeEmpty();
        await ShouldFailWithAsync(await client.DeleteAsync($"/recurring-payments/{id}"),
            HttpStatusCode.NotFound, "RecurringPayments.NotFound");
    }

    [Fact]
    public async Task List_OrdersActiveFirst_ThenDay_ThenName()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("rp-order@example.com", "rporder");
        await CreateAsync(client, new { name = "Inactive early", amount = 1m, dayOfMonth = 1, isActive = false });
        await CreateAsync(client, new { name = "B", amount = 1m, dayOfMonth = 9 });
        await CreateAsync(client, new { name = "Z", amount = 1m, dayOfMonth = 3 });
        await CreateAsync(client, new { name = "A", amount = 1m, dayOfMonth = 9 });

        (await ListAsync(client)).Select(i => i.Name)
            .ShouldBe(["Z", "A", "B", "Inactive early"]);
    }

    [Fact]
    public async Task Create_Validation_ReturnsCodes()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("rp-valid@example.com", "rpvalid");
        Guid catA = await CreateCategoryAsync(client, "RP Cat A");
        Guid catB = await CreateCategoryAsync(client, "RP Cat B");
        Guid subOfB = await CreateSubCategoryAsync(client, catB, "RP Sub B");

        async Task Fail(object body, string code) =>
            await ShouldFailWithAsync(await client.PostAsJsonAsync("/recurring-payments", body),
                HttpStatusCode.BadRequest, code);

        await Fail(new { name = " ", amount = 1m, dayOfMonth = 1 }, "RecurringPayments.NameRequired");
        await Fail(new { name = new string('x', 101), amount = 1m, dayOfMonth = 1 }, "RecurringPayments.NameTooLong");
        await Fail(new { name = "x", amount = -1m, dayOfMonth = 1 }, "RecurringPayments.NegativeAmount");
        await Fail(new { name = "x", amount = 1m, dayOfMonth = 0 }, "RecurringPayments.InvalidDay");
        await Fail(new { name = "x", amount = 1m, dayOfMonth = 32 }, "RecurringPayments.InvalidDay");
        await Fail(new { name = "x", amount = 1m, dayOfMonth = 1, categoryId = Guid.NewGuid() },
            "RecurringPayments.InvalidCategory");
        await Fail(new { name = "x", amount = 1m, dayOfMonth = 1, categoryId = catA, subCategoryId = subOfB },
            "RecurringPayments.InvalidSubCategory");
        await Fail(new { name = "x", amount = 1m, dayOfMonth = 1, subCategoryId = subOfB },
            "RecurringPayments.InvalidSubCategory");
        await Fail(new { name = "x", amount = 1m, dayOfMonth = 1, paymentMethod = "bitcoin" },
            "RecurringPayments.InvalidPaymentMethod");
        await Fail(new { name = "x", amount = 1m, dayOfMonth = 1, bank = new string('b', 101) },
            "RecurringPayments.BankTooLong");
        await Fail(new { name = "x", amount = 1m, dayOfMonth = 1, note = new string('n', 501) },
            "RecurringPayments.NoteTooLong");

        (await ListAsync(client)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Update_Validation_LeavesItemUntouched()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("rp-updvalid@example.com", "rpupdvalid");
        Guid id = await CreateAsync(client, Valid());

        await ShouldFailWithAsync(
            await client.PutAsJsonAsync($"/recurring-payments/{id}", new { name = "Changed", amount = 1m, dayOfMonth = 40 }),
            HttpStatusCode.BadRequest, "RecurringPayments.InvalidDay");
        (await ListAsync(client)).Single().Name.ShouldBe("Tiền điện");
    }

    [Fact]
    public async Task OtherUsersItem_Returns404_AndIsAbsentFromList()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync("rp-owner@example.com", "rpowner");
        HttpClient other = await CreateAuthenticatedClientAsync("rp-other@example.com", "rpother");
        Guid id = await CreateAsync(owner, Valid());

        (await ListAsync(other)).ShouldBeEmpty();
        await ShouldFailWithAsync(
            await other.PutAsJsonAsync($"/recurring-payments/{id}", new { name = "Hack", amount = 1m, dayOfMonth = 1 }),
            HttpStatusCode.NotFound, "RecurringPayments.NotFound");
        await ShouldFailWithAsync(await other.DeleteAsync($"/recurring-payments/{id}"),
            HttpStatusCode.NotFound, "RecurringPayments.NotFound");
        (await ListAsync(owner)).Single().Name.ShouldBe("Tiền điện");
    }
}
