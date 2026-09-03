using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Api.IntegrationTests.PurchasePlaces;

public sealed class PurchasePlacesEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
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
    internal sealed record PurchasePlaceBody(Guid Id, string Name);
    internal sealed record PlanListBody(Guid Id, string Name, bool IsDefault);

    private static async Task<Guid> CreatePurchasePlaceAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/purchase-places", new { name });
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

    private static async Task<Guid> CreateGoldTypeAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/gold-types", new { name });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    [Fact]
    public async Task GetPurchasePlaces_WithoutToken_Returns401()
    {
        (await factory.CreateClient().GetAsync("/purchase-places")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateAndList_Works()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("place-create@example.com", "placecreate");

        (await client.PostAsJsonAsync("/purchase-places", new { name = "SJC Trần Nhân Tông" })).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await client.PostAsJsonAsync("/purchase-places", new { name = "PNJ" })).StatusCode.ShouldBe(HttpStatusCode.Created);
        // Duplicate name (same user) is a conflict.
        (await client.PostAsJsonAsync("/purchase-places", new { name = "SJC Trần Nhân Tông" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        // Empty name is a validation error.
        (await client.PostAsJsonAsync("/purchase-places", new { name = " " })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var list = await (await client.GetAsync("/purchase-places")).Content.ReadFromJsonAsync<List<PurchasePlaceBody>>();
        list!.Count.ShouldBe(2);
        list.Select(p => p.Name).ShouldBe(new List<string> { "PNJ", "SJC Trần Nhân Tông" }); // ordered by name
    }

    [Fact]
    public async Task Rename_Works_AndChecksDuplicates()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("place-rename@example.com", "placerename");
        Guid pnj = await CreatePurchasePlaceAsync(client, "PNJ");
        await CreatePurchasePlaceAsync(client, "SJC");

        (await client.PutAsJsonAsync($"/purchase-places/{pnj}", new { name = "PNJ Quận 1" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        // Renaming onto an existing name is a conflict.
        (await client.PutAsJsonAsync($"/purchase-places/{pnj}", new { name = "SJC" }))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var list = await (await client.GetAsync("/purchase-places")).Content.ReadFromJsonAsync<List<PurchasePlaceBody>>();
        list!.Select(p => p.Name).ShouldContain("PNJ Quận 1");
    }

    [Fact]
    public async Task Delete_Guards()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("place-del@example.com", "placedel");
        Guid unused = await CreatePurchasePlaceAsync(client, "Trống");
        (await client.DeleteAsync($"/purchase-places/{unused}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Foreign user's purchase place is a 404.
        Guid mine = await CreatePurchasePlaceAsync(client, "Của tôi");
        HttpClient other = await CreateAuthenticatedClientAsync("place-other@example.com", "placeother");
        (await other.DeleteAsync($"/purchase-places/{mine}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_Guards_WhenReferencedByTransactionOrAcquisition()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("place-inuse@example.com", "placeinuse");
        Guid categoryId = await CreateCategoryAsync(client, "Chi PlaceInUse", "tag");
        Guid planId = await GetDefaultPlanIdAsync(client);
        Guid goldTypeId = await CreateGoldTypeAsync(client, "Nhẫn placeinuse");

        // Referenced by a transaction → 409; unlink then delete → 204.
        Guid txPlace = await CreatePurchasePlaceAsync(client, "Chỗ mua giao dịch");
        var createTx = await client.PostAsJsonAsync("/transactions", new
        {
            date = "2026-08-10", content = "Mua vàng", creditAmount = 0m, debitAmount = 5_000_000m,
            note = (string?)null, categoryId, planId, goldTypeId, goldQuantity = 0.5m, purchasePlaceId = txPlace
        });
        createTx.StatusCode.ShouldBe(HttpStatusCode.Created);
        Guid txId = (await createTx.Content.ReadFromJsonAsync<CreatedBody>())!.Id;

        (await client.DeleteAsync($"/purchase-places/{txPlace}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await client.PutAsJsonAsync($"/transactions/{txId}", new
        {
            date = "2026-08-10", content = "Mua vàng", creditAmount = 0m, debitAmount = 5_000_000m,
            note = (string?)null, categoryId, planId, goldTypeId, goldQuantity = 0.5m,
            purchasePlaceId = (Guid?)null
        })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/purchase-places/{txPlace}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Referenced by an acquisition → 409; unlink then delete → 204.
        Guid acqPlace = await CreatePurchasePlaceAsync(client, "Chỗ mua sổ vàng");
        var createAcq = await client.PostAsJsonAsync("/gold/acquisitions", new
        {
            goldTypeId, date = "2024-05-10", quantity = 1m, unitPrice = 5_000_000m, note = (string?)null,
            purchasePlaceId = acqPlace
        });
        createAcq.StatusCode.ShouldBe(HttpStatusCode.Created);
        Guid acqId = (await createAcq.Content.ReadFromJsonAsync<CreatedBody>())!.Id;

        (await client.DeleteAsync($"/purchase-places/{acqPlace}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await client.PutAsJsonAsync($"/gold/acquisitions/{acqId}", new
        {
            goldTypeId, date = "2024-05-10", quantity = 1m, unitPrice = 5_000_000m, note = (string?)null,
            purchasePlaceId = (Guid?)null
        })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/purchase-places/{acqPlace}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
