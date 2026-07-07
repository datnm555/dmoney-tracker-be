using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Users;
using Domain.Users;
using MockQueryable.NSubstitute;
using NSubstitute;
using Shouldly;

namespace Application.UnitTests.Users;

public class LoginCommandHandlerTests
{
    private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenProvider _tokenProvider = Substitute.For<ITokenProvider>();

    private LoginCommandHandler CreateHandler(User? existingUser = null)
    {
        List<User> users = existingUser is null ? [] : [existingUser];
        var usersDbSet = users.BuildMockDbSet();
        _dbContext.Users.Returns(usersDbSet);
        _tokenProvider.Create(Arg.Is<User>(u => u != null)).ReturnsForAnyArgs("jwt-token");
        return new LoginCommandHandler(_dbContext, _passwordHasher, _tokenProvider);
    }

    [Fact]
    public async Task Handle_WithCorrectPassword_ReturnsToken()
    {
        User user = User.Create("a@b.com", "user1", "User", "stored-hash").Value;
        _passwordHasher.Verify("password123", "stored-hash").Returns(true);
        var handler = CreateHandler(user);

        var result = await handler.Handle(new LoginCommand("a@b.com", "password123"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Token.ShouldBe("jwt-token");
        result.Value.Username.ShouldBe("user1");
    }

    [Fact]
    public async Task Handle_WithUsernameIdentifier_ReturnsToken()
    {
        User user = User.Create("a@b.com", "user1", "User", "stored-hash").Value;
        _passwordHasher.Verify("password123", "stored-hash").Returns(true);
        var handler = CreateHandler(user);

        var result = await handler.Handle(new LoginCommand("USER1", "password123"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_WithWrongPassword_FailsUnauthorized()
    {
        User user = User.Create("a@b.com", "user1", "User", "stored-hash").Value;
        _passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var handler = CreateHandler(user);

        var result = await handler.Handle(new LoginCommand("a@b.com", "wrong"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Users.InvalidCredentials");
    }

    [Fact]
    public async Task Handle_WithUnknownUser_FailsUnauthorized()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new LoginCommand("ghost@b.com", "password123"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Users.InvalidCredentials");
    }
}
