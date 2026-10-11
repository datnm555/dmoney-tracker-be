using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.IntegrationTests.Infrastructure;
using Domain.Categories;
using Domain.SubCategories;
using global::Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Api.IntegrationTests.SubCategories;

public sealed class SubCategoriesEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
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

    /// <summary>
    /// A fresh test db has no seeded savings category / savings-book sub-category (the API
    /// cannot create coded ones), so seed them directly (idempotent).
    /// </summary>
    private async Task<(Guid CategoryId, Guid SubCategoryId)> SeedSavingsBookAsync()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Category? savings = await dbContext.Categories.FirstOrDefaultAsync(c => c.Code == "savings");
        if (savings is null)
        {
            savings = Category.Create("Tích luỹ", "piggy-bank", "system", "savings", CategoryKinds.Both).Value;
            dbContext.Categories.Add(savings);
        }

        SubCategory? book = await dbContext.SubCategories
            .FirstOrDefaultAsync(s => s.Code == SubCategoryCodes.SavingsBook);
        if (book is null)
        {
            book = SubCategory.Create(savings.Id, "Sổ tiết kiệm", "system", code: SubCategoryCodes.SavingsBook).Value;
            dbContext.SubCategories.Add(book);
        }

        await dbContext.SaveChangesAsync();
        return (savings.Id, book.Id);
    }

    internal sealed record LoginBody(string Token, Guid UserId, string Email, string Username, string DisplayName);
    internal sealed record CreatedBody(Guid Id);
    internal sealed record SubCategoryBody(
        Guid Id, Guid CategoryId, string Name, bool IsDefault, string? Icon, string? Code);
    internal sealed record ErrorBody(string Code, string Description);

    [Fact]
    public async Task GetSubCategories_ReturnsCode_ForCodedAndNullForUserCreated()
    {
        (Guid savingsId, Guid bookId) = await SeedSavingsBookAsync();
        HttpClient client = await CreateAuthenticatedClientAsync("sub-get@example.com", "subget");

        var created = await client.PostAsJsonAsync("/subcategories", new { categoryId = savingsId, name = "Vàng" });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        Guid goldId = (await created.Content.ReadFromJsonAsync<CreatedBody>())!.Id;

        var list = await (await client.GetAsync($"/subcategories?categoryId={savingsId}"))
            .Content.ReadFromJsonAsync<List<SubCategoryBody>>();

        list.ShouldNotBeNull();
        list.Single(s => s.Id == bookId).Code.ShouldBe("savings_book");
        list.Single(s => s.Id == goldId).Code.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteSubCategory_Coded_Returns400SystemSubCategory()
    {
        (_, Guid bookId) = await SeedSavingsBookAsync();
        HttpClient client = await CreateAuthenticatedClientAsync("sub-del@example.com", "subdel");

        var response = await client.DeleteAsync($"/subcategories/{bookId}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorBody>();
        error!.Code.ShouldBe("SubCategories.SystemSubCategory");
        error.Description.ShouldBe("Không thể xóa danh mục con hệ thống.");

        var list = await (await client.GetAsync("/subcategories"))
            .Content.ReadFromJsonAsync<List<SubCategoryBody>>();
        list!.ShouldContain(s => s.Id == bookId);
    }

    [Fact]
    public async Task DeleteSubCategory_UserCreated_StillWorks()
    {
        (Guid savingsId, _) = await SeedSavingsBookAsync();
        HttpClient client = await CreateAuthenticatedClientAsync("sub-del2@example.com", "subdel2");
        var created = await client.PostAsJsonAsync("/subcategories", new { categoryId = savingsId, name = "Tạm" });
        Guid id = (await created.Content.ReadFromJsonAsync<CreatedBody>())!.Id;

        (await client.DeleteAsync($"/subcategories/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateSubCategory_Coded_CanBeRenamed_KeepsCode()
    {
        (_, Guid bookId) = await SeedSavingsBookAsync();
        HttpClient client = await CreateAuthenticatedClientAsync("sub-ren@example.com", "subren");

        var response = await client.PutAsJsonAsync($"/subcategories/{bookId}", new { name = "Sổ TK ngân hàng" });
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var list = await (await client.GetAsync("/subcategories"))
            .Content.ReadFromJsonAsync<List<SubCategoryBody>>();
        SubCategoryBody book = list!.Single(s => s.Id == bookId);
        book.Name.ShouldBe("Sổ TK ngân hàng");
        book.Code.ShouldBe("savings_book");

        (await client.PutAsJsonAsync($"/subcategories/{bookId}", new { name = "Sổ tiết kiệm" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
