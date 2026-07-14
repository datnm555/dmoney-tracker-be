using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Users;
using Domain.Users;
using MockQueryable.NSubstitute;
using NSubstitute;
using Shouldly;

namespace Application.UnitTests.Users;

public class RegisterUserCommandHandlerTests
{
    private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();

    private RegisterUserCommandHandler CreateHandler(params User[] existingUsers)
    {
        var usersDbSet = existingUsers.ToList().BuildMockDbSet();
        _dbContext.Users.Returns(usersDbSet);
        var categoriesDbSet = new List<Domain.Categories.Category>().BuildMockDbSet();
        _dbContext.Categories.Returns(categoriesDbSet);
        _passwordHasher.Hash(Arg.Any<string>()).Returns("hashed");
        return new RegisterUserCommandHandler(_dbContext, _passwordHasher);
    }

    private static User ExistingUser() =>
        User.Create("taken@example.com", "taken", "Taken", "hash").Value;

    [Fact]
    public async Task Handle_WithValidCommand_CreatesUser()
    {
        var handler = CreateHandler();
        var command = new RegisterUserCommand("new@example.com", "newuser", "New User", "password123");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBe(Guid.Empty);
        // Registration also seeds the built-in categories for the new user.
        _dbContext.Categories.Received(8).Add(Arg.Any<Domain.Categories.Category>());
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithShortPassword_Fails()
    {
        var handler = CreateHandler();
        var command = new RegisterUserCommand("new@example.com", "newuser", "New User", "short");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Users.PasswordTooShort");
        await _dbContext.DidNotReceiveWithAnyArgs().SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WithDuplicateEmail_Fails()
    {
        var handler = CreateHandler(ExistingUser());
        var command = new RegisterUserCommand("Taken@Example.com", "newuser", "New User", "password123");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Users.EmailNotUnique");
    }

    [Fact]
    public async Task Handle_WithDuplicateUsername_Fails()
    {
        var handler = CreateHandler(ExistingUser());
        var command = new RegisterUserCommand("new@example.com", "TAKEN", "New User", "password123");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Users.UsernameNotUnique");
    }
}
