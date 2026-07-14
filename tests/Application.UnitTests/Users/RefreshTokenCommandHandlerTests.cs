using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Users;
using Domain.Users;
using MockQueryable.NSubstitute;
using NSubstitute;
using SharedKernel;
using Shouldly;

namespace Application.UnitTests.Users;

public class RefreshTokenCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);

    private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
    private readonly ITokenProvider _tokenProvider = Substitute.For<ITokenProvider>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();

    private RefreshTokenCommandHandler CreateHandler(User? user = null, params RefreshToken[] tokens)
    {
        var usersDbSet = (user is null ? new List<User>() : [user]).BuildMockDbSet();
        _dbContext.Users.Returns(usersDbSet);
        var tokensDbSet = tokens.ToList().BuildMockDbSet();
        _dbContext.RefreshTokens.Returns(tokensDbSet);
        _tokenProvider.Create(Arg.Is<User>(u => u != null)).ReturnsForAnyArgs("new-jwt");
        _clock.UtcNow.Returns(Now);
        return new RefreshTokenCommandHandler(_dbContext, _tokenProvider, _clock);
    }

    private static User UserWithId(out Guid id)
    {
        User user = User.Create("a@b.com", "user1", "User", "hash").Value;
        id = user.Id;
        return user;
    }

    [Fact]
    public async Task Handle_ValidToken_RotatesAndReturnsNewTokens()
    {
        User user = UserWithId(out Guid userId);
        RefreshToken stored = RefreshToken.Create(userId, "old-token", Now.AddDays(-1));
        var handler = CreateHandler(user, stored);

        var result = await handler.Handle(new RefreshTokenCommand("old-token"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Token.ShouldBe("new-jwt");
        result.Value.RefreshToken.ShouldNotBeNullOrEmpty();
        result.Value.RefreshToken.ShouldNotBe("old-token");
        _dbContext.RefreshTokens.Received(1).Remove(stored);
        _dbContext.RefreshTokens.Received(1).Add(Arg.Any<RefreshToken>());
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExpiredToken_Fails()
    {
        User user = UserWithId(out Guid userId);
        RefreshToken stored = RefreshToken.Create(userId, "old-token", Now.AddDays(-31));
        var handler = CreateHandler(user, stored);

        var result = await handler.Handle(new RefreshTokenCommand("old-token"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task Handle_UnknownToken_Fails()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new RefreshTokenCommand("missing"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task Logout_RemovesTheStoredToken()
    {
        User user = UserWithId(out Guid userId);
        RefreshToken stored = RefreshToken.Create(userId, "old-token", Now);
        CreateHandler(user, stored);
        var logout = new LogoutCommandHandler(_dbContext);

        var result = await logout.Handle(new LogoutCommand("old-token"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _dbContext.RefreshTokens.Received(1).Remove(stored);
    }
}
