using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Api.IntegrationTests.Plans;

public sealed class PlansEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
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
    internal sealed record PlanBody(Guid Id, string Name, bool IsDefault);

    [Fact]
    public async Task GetPlans_WithoutToken_Returns401()
    {
        (await factory.CreateClient().GetAsync("/plans")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_CreatesDefaultPlan()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("plan-default@example.com", "plandefault");

        var response = await client.GetAsync("/plans");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var plans = await response.Content.ReadFromJsonAsync<List<PlanBody>>();
        plans.ShouldNotBeNull();
        plans.Count.ShouldBe(1);
        plans[0].Name.ShouldBe("Sổ chính");
        plans[0].IsDefault.ShouldBeTrue();
    }
}
