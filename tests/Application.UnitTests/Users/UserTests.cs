using Domain.Users;
using Shouldly;

namespace Application.UnitTests.Users;

public class UserTests
{
    [Fact]
    public void Create_WithValidInput_NormalizesEmailAndUsername()
    {
        var result = User.Create("  Dat@Example.COM ", " DatNM555 ", " Đạt ", "hash");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Email.ShouldBe("dat@example.com");
        result.Value.Username.ShouldBe("datnm555");
        result.Value.DisplayName.ShouldBe("Đạt");
        result.Value.PasswordHash.ShouldBe("hash");
        result.Value.Id.ShouldNotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("a@b")]
    public void Create_WithInvalidEmail_Fails(string email)
    {
        var result = User.Create(email, "user1", "User", "hash");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Users.InvalidEmail");
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("has space")]
    [InlineData("ThirtyOneCharacters31Characters")]
    public void Create_WithInvalidUsername_Fails(string username)
    {
        var result = User.Create("a@b.com", username, "User", "hash");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Users.InvalidUsername");
    }

    [Fact]
    public void Create_WithBlankDisplayName_Fails()
    {
        var result = User.Create("a@b.com", "user1", "   ", "hash");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Users.InvalidDisplayName");
    }
}
