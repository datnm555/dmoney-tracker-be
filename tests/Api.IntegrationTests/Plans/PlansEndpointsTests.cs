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

    private static async Task<Guid> CreatePlanAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/plans", new { name });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    private static async Task<Guid> CreateCategoryAsync(HttpClient client, string name, string icon)
    {
        var response = await client.PostAsJsonAsync("/categories", new { name, icon });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedBody>())!.Id;
    }

    private sealed record LoginBody(string Token);
    internal sealed record PlanBody(Guid Id, string Name, bool IsDefault);
    internal sealed record CreatedBody(Guid Id);

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

    [Fact]
    public async Task CreatePlan_AppearsAfterDefault()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("plan-create@example.com", "plancreate");

        var create = await client.PostAsJsonAsync("/plans", new { name = "Du lịch Đà Nẵng" });
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<CreatedBody>();
        created.ShouldNotBeNull();

        var plans = (await (await client.GetAsync("/plans")).Content.ReadFromJsonAsync<List<PlanBody>>())!;
        plans.Count.ShouldBe(2);
        plans[0].IsDefault.ShouldBeTrue();
        plans[1].Id.ShouldBe(created.Id);
        plans[1].Name.ShouldBe("Du lịch Đà Nẵng");
        plans[1].IsDefault.ShouldBeFalse();
    }

    [Fact]
    public async Task CreatePlan_EmptyName_Returns400()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("plan-empty@example.com", "planempty");
        var create = await client.PostAsJsonAsync("/plans", new { name = "  " });
        create.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RenamePlan_Works()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("plan-rename@example.com", "planrename");
        Guid planId = await CreatePlanAsync(client, "Cũ");

        (await client.PutAsJsonAsync($"/plans/{planId}", new { name = "Mới" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var plans = (await (await client.GetAsync("/plans")).Content.ReadFromJsonAsync<List<PlanBody>>())!;
        plans.ShouldContain(p => p.Name == "Mới");
    }

    [Fact]
    public async Task SetDefaultPlan_MovesDefaultBetweenPlans()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("plan-setdef@example.com", "plansetdef");
        Guid newPlan = await CreatePlanAsync(client, "Sổ phụ");

        (await client.PutAsync($"/plans/{newPlan}/default", null))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var plans = (await (await client.GetAsync("/plans")).Content.ReadFromJsonAsync<List<PlanBody>>())!;
        plans.Single(p => p.IsDefault).Id.ShouldBe(newPlan);

        // Idempotent on the plan that is already the default.
        (await client.PutAsync($"/plans/{newPlan}/default", null))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // The old default is now deletable, the new one is protected.
        Guid oldDefault = plans.Single(p => !p.IsDefault).Id;
        (await client.DeleteAsync($"/plans/{newPlan}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await client.DeleteAsync($"/plans/{oldDefault}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Someone else's plan is a 404.
        HttpClient other = await CreateAuthenticatedClientAsync("plan-setdef2@example.com", "plansetdef2");
        (await other.PutAsync($"/plans/{newPlan}/default", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletePlan_Guards()
    {
        HttpClient client = await CreateAuthenticatedClientAsync("plan-delete@example.com", "plandelete");
        Guid defaultPlan = (await (await client.GetAsync("/plans")).Content
            .ReadFromJsonAsync<List<PlanBody>>())![0].Id;

        // Default plan is never deletable.
        (await client.DeleteAsync($"/plans/{defaultPlan}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // A plan with transactions is not deletable.
        Guid full = await CreatePlanAsync(client, "Có giao dịch");
        Guid categoryId = await CreateCategoryAsync(client, "Chi Delete", "tag");
        (await client.PostAsJsonAsync("/transactions", new
        {
            date = "2026-07-05",
            content = "Chi",
            creditAmount = 0m,
            debitAmount = 100_000m,
            note = (string?)null,
            categoryId,
            planId = full
        })).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await client.DeleteAsync($"/plans/{full}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // An empty non-default plan deletes fine.
        Guid empty = await CreatePlanAsync(client, "Trống");
        (await client.DeleteAsync($"/plans/{empty}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Someone else's plan is a 404.
        HttpClient other = await CreateAuthenticatedClientAsync("plan-other@example.com", "planother");
        (await other.DeleteAsync($"/plans/{full}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
