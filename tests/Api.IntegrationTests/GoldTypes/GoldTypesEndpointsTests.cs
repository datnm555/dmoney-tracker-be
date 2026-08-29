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

    private static async Task<Guid> CreateGoldTypeAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/gold-types", new { name });
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
}
