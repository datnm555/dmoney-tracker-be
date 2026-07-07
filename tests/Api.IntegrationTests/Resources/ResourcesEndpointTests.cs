using System.Net;
using System.Net.Http.Json;
using Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Api.IntegrationTests.Resources;

public sealed class ResourcesEndpointTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetResources_DefaultsToVietnamese()
    {
        var response = await _client.GetAsync("/resources");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var resources = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        resources.ShouldNotBeNull();
        resources["menu.summary"].ShouldBe("Tổng hợp");
        resources["Transactions.EmptyAmount"].ShouldBe("Cần ít nhất một số tiền lớn hơn 0.");
    }

    [Fact]
    public async Task GetResources_WithLangEn_ReturnsEnglish()
    {
        var response = await _client.GetAsync("/resources?lang=en");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var resources = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        resources.ShouldNotBeNull();
        resources["menu.summary"].ShouldBe("Summary");
    }

    [Fact]
    public async Task Login_Error_IsLocalizedByLangParam()
    {
        var vi = await _client.PostAsJsonAsync("/users/login?lang=vi",
            new { identifier = "ghost@example.com", password = "password123" });
        var viBody = await vi.Content.ReadAsStringAsync();
        viBody.ShouldContain("Thông tin đăng nhập không đúng.");

        var en = await _client.PostAsJsonAsync("/users/login?lang=en",
            new { identifier = "ghost@example.com", password = "password123" });
        var enBody = await en.Content.ReadAsStringAsync();
        enBody.ShouldContain("Invalid credentials.");
    }
}
