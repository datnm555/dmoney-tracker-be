using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Api.IntegrationTests.GoldTypes;

public sealed class GoldTypesEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
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
    internal sealed record GoldTypeBody(Guid Id, string Name);
    internal sealed record PlanListBody(Guid Id, string Name, bool IsDefault);

    private static async Task<Guid> CreateGoldTypeAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/gold-types", new { name });
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

    private static async Task<Guid> CreateAcquisitionAsync(HttpClient client, Guid goldTypeId)
    {
        var response = await client.PostAsJsonAsync("/gold/acquisitions", new
        {
            goldTypeId, date = "2024-05-10", quantity = 1m, unitPrice = 5_000_000m, note = (string?)null
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    [Fact]
    public async Task GetGoldTypes_WithoutToken_Returns401()
    {
        (await factory.CreateClient().GetAsync("/gold-types")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateAndList_Works()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("gold-create@example.com", "goldcreate");

        (await client.PostAsJsonAsync("/gold-types", new { name = "SJC miếng" })).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await client.PostAsJsonAsync("/gold-types", new { name = "Nhẫn trơn 9999" })).StatusCode.ShouldBe(HttpStatusCode.Created);
        // Duplicate name (same user) is a conflict.
        (await client.PostAsJsonAsync("/gold-types", new { name = "SJC miếng" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        // Empty name is a validation error.
        (await client.PostAsJsonAsync("/gold-types", new { name = " " })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var list = await (await client.GetAsync("/gold-types")).Content.ReadFromJsonAsync<List<GoldTypeBody>>();
        list!.Count.ShouldBe(2);
        list.Select(g => g.Name).ShouldBe(new List<string> { "Nhẫn trơn 9999", "SJC miếng" }); // ordered by name
    }

    [Fact]
    public async Task Rename_Works_AndChecksDuplicates()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("gold-rename@example.com", "goldrename");
        Guid ring = await CreateGoldTypeAsync(client, "Nhẫn trơn");
        await CreateGoldTypeAsync(client, "SJC");

        (await client.PutAsJsonAsync($"/gold-types/{ring}", new { name = "Nhẫn trơn 9999" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        // Renaming onto an existing name is a conflict.
        (await client.PutAsJsonAsync($"/gold-types/{ring}", new { name = "SJC" }))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var list = await (await client.GetAsync("/gold-types")).Content.ReadFromJsonAsync<List<GoldTypeBody>>();
        list!.Select(g => g.Name).ShouldContain("Nhẫn trơn 9999");
    }

    [Fact]
    public async Task Delete_Guards()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("gold-del@example.com", "golddel");
        Guid unused = await CreateGoldTypeAsync(client, "Trống");
        (await client.DeleteAsync($"/gold-types/{unused}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Foreign user's gold type is a 404.
        Guid mine = await CreateGoldTypeAsync(client, "Của tôi");
        HttpClient other = await CreateAuthenticatedClientAsync("gold-other@example.com", "goldother");
        (await other.DeleteAsync($"/gold-types/{mine}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_InUse_ThenUnlink_Allows()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("gold-inuse@example.com", "goldinuse");
        Guid goldTypeId = await CreateGoldTypeAsync(client, "Đang dùng");
        Guid planId = await GetDefaultPlanIdAsync(client);
        Guid categoryId = await CreateCategoryAsync(client, "Chi GInUse", "tag");

        var create = await client.PostAsJsonAsync("/transactions", new
        {
            date = "2026-08-10", content = "Mua vàng", creditAmount = 0m, debitAmount = 10_000_000m,
            note = (string?)null, categoryId, planId, goldTypeId, goldQuantity = 1m
        });
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        Guid transactionId = (await create.Content.ReadFromJsonAsync<CreatedBody>())!.Id;

        (await client.DeleteAsync($"/gold-types/{goldTypeId}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var update = await client.PutAsJsonAsync($"/transactions/{transactionId}", new
        {
            date = "2026-08-10", content = "Mua vàng", creditAmount = 0m, debitAmount = 10_000_000m,
            note = (string?)null, categoryId, planId
        });
        update.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.DeleteAsync($"/gold-types/{goldTypeId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_BlockedByAcquisition_ThenAllowed()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("gold-acqguard@example.com", "goldacqguard");
        Guid goldTypeId = await CreateGoldTypeAsync(client, "Có acquisition");

        Guid acquisitionId = await CreateAcquisitionAsync(client, goldTypeId);
        (await client.DeleteAsync($"/gold-types/{goldTypeId}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await client.DeleteAsync($"/gold/acquisitions/{acquisitionId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/gold-types/{goldTypeId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
