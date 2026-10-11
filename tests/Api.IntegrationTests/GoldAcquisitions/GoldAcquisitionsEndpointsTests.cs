using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Api.IntegrationTests.GoldAcquisitions;

public sealed class GoldAcquisitionsEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
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
    internal sealed record MoneyBody(decimal Amount, string Currency);
    internal sealed record AcquisitionBody(
        Guid Id, string Date, Guid GoldTypeId, string GoldTypeName,
        decimal Quantity, MoneyBody UnitPrice, MoneyBody Value, string? Note,
        Guid? PurchasePlaceId, string? PurchasePlaceName);

    private static async Task<Guid> CreateGoldTypeAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/gold-types", new { name });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    private static async Task<Guid> CreatePurchasePlaceAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/purchase-places", new { name });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    [Fact]
    public async Task GetAcquisitions_WithoutToken_Returns401()
    {
        (await factory.CreateClient().GetAsync("/gold/acquisitions")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateListUpdateDelete_Works()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("goldacq-crud@example.com", "goldacqcrud");
        Guid goldTypeId = await CreateGoldTypeAsync(client, "Nhẫn cũ");

        var createBuy = await client.PostAsJsonAsync("/gold/acquisitions", new
        {
            goldTypeId,
            date = "2024-05-10",
            quantity = 3m,
            unitPrice = 5_500_000m,
            note = "mua 2024"
        });
        createBuy.StatusCode.ShouldBe(HttpStatusCode.Created);
        Guid buyId = (await createBuy.Content.ReadFromJsonAsync<CreatedBody>())!.Id;

        var createGift = await client.PostAsJsonAsync("/gold/acquisitions", new
        {
            goldTypeId,
            date = "2024-06-01",
            quantity = 2m,
            unitPrice = 0m,
            note = "được tặng"
        });
        createGift.StatusCode.ShouldBe(HttpStatusCode.Created);
        Guid giftId = (await createGift.Content.ReadFromJsonAsync<CreatedBody>())!.Id;

        var list1 = await (await client.GetAsync("/gold/acquisitions")).Content
            .ReadFromJsonAsync<List<AcquisitionBody>>();
        list1!.Count.ShouldBe(2);
        list1[0].Id.ShouldBe(giftId); // most recent date first
        list1[0].Value.Amount.ShouldBe(0m);
        list1[0].GoldTypeName.ShouldBe("Nhẫn cũ");
        list1[1].Id.ShouldBe(buyId);
        list1[1].Value.Amount.ShouldBe(16_500_000m);
        list1[1].GoldTypeName.ShouldBe("Nhẫn cũ");

        var update = await client.PutAsJsonAsync($"/gold/acquisitions/{giftId}", new
        {
            goldTypeId,
            date = "2024-06-01",
            quantity = 2.5m,
            unitPrice = 5_600_000m,
            note = "được tặng"
        });
        update.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var list2 = await (await client.GetAsync("/gold/acquisitions")).Content
            .ReadFromJsonAsync<List<AcquisitionBody>>();
        list2!.Single(a => a.Id == giftId).Value.Amount.ShouldBe(14_000_000m);

        (await client.DeleteAsync($"/gold/acquisitions/{giftId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var list3 = await (await client.GetAsync("/gold/acquisitions")).Content
            .ReadFromJsonAsync<List<AcquisitionBody>>();
        list3!.Count.ShouldBe(1);
        list3[0].Id.ShouldBe(buyId);
    }

    [Fact]
    public async Task Validation_Works()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("goldacq-valid@example.com", "goldacqvalid");
        Guid goldTypeId = await CreateGoldTypeAsync(client, "SJC");

        (await client.PostAsJsonAsync("/gold/acquisitions", new
        {
            goldTypeId,
            date = "2024-01-01",
            quantity = 0m,
            unitPrice = 100_000m,
            note = (string?)null
        })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await client.PostAsJsonAsync("/gold/acquisitions", new
        {
            goldTypeId,
            date = "2024-01-01",
            quantity = 1m,
            unitPrice = -1m,
            note = (string?)null
        })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await client.PostAsJsonAsync("/gold/acquisitions", new
        {
            goldTypeId,
            date = "2024-01-01",
            quantity = 1m,
            unitPrice = 100_000m,
            note = new string('a', 256)
        })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await client.PostAsJsonAsync("/gold/acquisitions", new
        {
            goldTypeId = Guid.NewGuid(),
            date = "2024-01-01",
            quantity = 1m,
            unitPrice = 100_000m,
            note = (string?)null
        })).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var create = await client.PostAsJsonAsync("/gold/acquisitions", new
        {
            goldTypeId,
            date = "2024-01-01",
            quantity = 1m,
            unitPrice = 100_000m,
            note = (string?)null
        });
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        Guid mineId = (await create.Content.ReadFromJsonAsync<CreatedBody>())!.Id;

        HttpClient other = await CreateAuthenticatedClientAsync("goldacq-other@example.com", "goldacqother");

        (await other.PutAsJsonAsync($"/gold/acquisitions/{mineId}", new
        {
            goldTypeId,
            date = "2024-01-01",
            quantity = 1m,
            unitPrice = 100_000m,
            note = (string?)null
        })).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await other.DeleteAsync($"/gold/acquisitions/{mineId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PurchasePlace_RoundTrips_AndUpdatePathThreads()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("goldacq-place@example.com", "goldacqplace");
        Guid goldTypeId = await CreateGoldTypeAsync(client, "Nhẫn acq place");
        Guid placeId = await CreatePurchasePlaceAsync(client, "SJC acq");

        var create = await client.PostAsJsonAsync("/gold/acquisitions", new
        {
            goldTypeId,
            date = "2024-05-10",
            quantity = 1m,
            unitPrice = 5_000_000m,
            note = (string?)null,
            purchasePlaceId = placeId
        });
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        Guid id = (await create.Content.ReadFromJsonAsync<CreatedBody>())!.Id;

        var list = await (await client.GetAsync("/gold/acquisitions")).Content
            .ReadFromJsonAsync<List<AcquisitionBody>>();
        AcquisitionBody item = list!.Single(a => a.Id == id);
        item.PurchasePlaceId.ShouldBe(placeId);
        item.PurchasePlaceName.ShouldBe("SJC acq");

        // Create without a place, then PUT with one — guards the explicit request-DTO threading.
        var createNoPlace = await client.PostAsJsonAsync("/gold/acquisitions", new
        {
            goldTypeId,
            date = "2024-06-01",
            quantity = 1m,
            unitPrice = 5_000_000m,
            note = (string?)null
        });
        createNoPlace.StatusCode.ShouldBe(HttpStatusCode.Created);
        Guid id2 = (await createNoPlace.Content.ReadFromJsonAsync<CreatedBody>())!.Id;

        (await client.PutAsJsonAsync($"/gold/acquisitions/{id2}", new
        {
            goldTypeId,
            date = "2024-06-01",
            quantity = 1m,
            unitPrice = 5_000_000m,
            note = (string?)null,
            purchasePlaceId = placeId
        })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var list2 = await (await client.GetAsync("/gold/acquisitions")).Content
            .ReadFromJsonAsync<List<AcquisitionBody>>();
        list2!.Single(a => a.Id == id2).PurchasePlaceId.ShouldBe(placeId);
    }

    [Fact]
    public async Task PurchasePlace_Unknown_Returns404()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("goldacq-placeunk@example.com", "goldacqplaceunk");
        Guid goldTypeId = await CreateGoldTypeAsync(client, "Nhẫn placeunk");

        (await client.PostAsJsonAsync("/gold/acquisitions", new
        {
            goldTypeId,
            date = "2024-01-01",
            quantity = 1m,
            unitPrice = 100_000m,
            note = (string?)null,
            purchasePlaceId = Guid.NewGuid()
        })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
