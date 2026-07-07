using System.Net;
using System.Net.Http.Json;
using Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Api.IntegrationTests.Users;

public sealed class UsersEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Register_ThenLogin_ReturnsToken()
    {
        var register = await _client.PostAsJsonAsync("/users/register",
            new { email = "dat@example.com", username = "datnm", displayName = "Đạt", password = "password123" });
        register.StatusCode.ShouldBe(HttpStatusCode.OK);

        var login = await _client.PostAsJsonAsync("/users/login",
            new { identifier = "dat@example.com", password = "password123" });

        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await login.Content.ReadFromJsonAsync<LoginBody>();
        body.ShouldNotBeNull();
        body.Token.ShouldNotBeNullOrWhiteSpace();
        body.Username.ShouldBe("datnm");
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409()
    {
        await _client.PostAsJsonAsync("/users/register",
            new { email = "dup@example.com", username = "dupuser1", displayName = "Dup", password = "password123" });

        var second = await _client.PostAsJsonAsync("/users/register",
            new { email = "dup@example.com", username = "dupuser2", displayName = "Dup", password = "password123" });

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        await _client.PostAsJsonAsync("/users/register",
            new { email = "wrong@example.com", username = "wrongpw", displayName = "W", password = "password123" });

        var login = await _client.PostAsJsonAsync("/users/login",
            new { identifier = "wrong@example.com", password = "bad-password" });

        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    internal sealed record LoginBody(string Token, Guid UserId, string Email, string Username, string DisplayName);
}
