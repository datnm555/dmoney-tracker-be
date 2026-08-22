using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Api.IntegrationTests.Beneficiaries;

public sealed class BeneficiariesEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
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
    internal sealed record BeneficiaryBody(Guid Id, string Name, bool IsDefault);
    internal sealed record CreatedBody(Guid Id);

    private static async Task<Guid> CreateBeneficiaryAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/beneficiaries", new { name });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    [Fact]
    public async Task GetBeneficiaries_WithoutToken_Returns401()
    {
        (await factory.CreateClient().GetAsync("/beneficiaries")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateAndList_Works()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("ben-create@example.com", "bencreate");

        var create = await client.PostAsJsonAsync("/beneficiaries", new { name = "Tôi" });
        create.StatusCode.ShouldBe(HttpStatusCode.Created);

        (await client.PostAsJsonAsync("/beneficiaries", new { name = "Vợ" })).StatusCode.ShouldBe(HttpStatusCode.Created);
        // Duplicate name (same user) is a conflict.
        (await client.PostAsJsonAsync("/beneficiaries", new { name = "Tôi" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        // Empty name is a validation error.
        (await client.PostAsJsonAsync("/beneficiaries", new { name = " " })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var list = await (await client.GetAsync("/beneficiaries")).Content.ReadFromJsonAsync<List<BeneficiaryBody>>();
        list!.Count.ShouldBe(2);
        list.Select(b => b.Name).ShouldBe(new List<string> { "Tôi", "Vợ" }); // no default yet → plain name order
        list.All(b => !b.IsDefault).ShouldBeTrue();
    }

    [Fact]
    public async Task RenameAndSetDefault_Work()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("ben-def@example.com", "bendef");
        Guid me = await CreateBeneficiaryAsync(client, "Tôi");
        Guid wife = await CreateBeneficiaryAsync(client, "Vợ");

        (await client.PutAsJsonAsync($"/beneficiaries/{me}", new { name = "Bản thân" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.PutAsync($"/beneficiaries/{wife}/default", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var list = await (await client.GetAsync("/beneficiaries")).Content.ReadFromJsonAsync<List<BeneficiaryBody>>();
        list![0].Id.ShouldBe(wife);               // default first
        list[0].IsDefault.ShouldBeTrue();
        list.Count(b => b.IsDefault).ShouldBe(1);

        // Default moves when set on another.
        (await client.PutAsync($"/beneficiaries/{me}/default", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        list = await (await client.GetAsync("/beneficiaries")).Content.ReadFromJsonAsync<List<BeneficiaryBody>>();
        list!.Single(b => b.IsDefault).Id.ShouldBe(me);
    }

    [Fact]
    public async Task Delete_Guards()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("ben-del@example.com", "bendel");
        Guid unused = await CreateBeneficiaryAsync(client, "Trống");
        (await client.DeleteAsync($"/beneficiaries/{unused}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Foreign user's beneficiary is a 404.
        Guid mine = await CreateBeneficiaryAsync(client, "Của tôi");
        HttpClient other = await CreateAuthenticatedClientAsync("ben-other@example.com", "benother");
        (await other.DeleteAsync($"/beneficiaries/{mine}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
