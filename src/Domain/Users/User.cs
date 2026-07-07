using System.Text.RegularExpressions;
using SharedKernel;

namespace Domain.Users;

public sealed partial class User : AggregateRoot
{
    private User() { }

    public string Email { get; private set; } = string.Empty;

    public string Username { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public static Result<User> Create(
        string email,
        string username,
        string displayName,
        string passwordHash)
    {
        string normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedEmail.Length is 0 or > 256 || !EmailRegex().IsMatch(normalizedEmail))
        {
            return Result.Failure<User>(UserErrors.InvalidEmail);
        }

        string normalizedUsername = (username ?? string.Empty).Trim().ToLowerInvariant();
        if (!UsernameRegex().IsMatch(normalizedUsername))
        {
            return Result.Failure<User>(UserErrors.InvalidUsername);
        }

        string trimmedDisplayName = (displayName ?? string.Empty).Trim();
        if (trimmedDisplayName.Length is 0 or > 100)
        {
            return Result.Failure<User>(UserErrors.InvalidDisplayName);
        }

        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Email = normalizedEmail,
            Username = normalizedUsername,
            DisplayName = trimmedDisplayName,
            PasswordHash = passwordHash
        };

        user.Raise(new UserRegisteredDomainEvent(user.Id, user.Email));

        return user;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();

    [GeneratedRegex("^[a-z0-9]{3,30}$")]
    private static partial Regex UsernameRegex();
}
