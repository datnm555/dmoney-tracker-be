using SharedKernel;

namespace Domain.Users;

public static class UserErrors
{
    public static readonly Error InvalidEmail = Error.Validation(
        "Users.InvalidEmail",
        "Email is not valid.");

    public static readonly Error InvalidUsername = Error.Validation(
        "Users.InvalidUsername",
        "Username must be 3-30 characters, letters and digits only.");

    public static readonly Error InvalidDisplayName = Error.Validation(
        "Users.InvalidDisplayName",
        "Display name must not be empty.");

    public static readonly Error PasswordTooShort = Error.Validation(
        "Users.PasswordTooShort",
        "Password must be at least 8 characters.");

    public static readonly Error EmailNotUnique = Error.Conflict(
        "Users.EmailNotUnique",
        "This email is already registered.");

    public static readonly Error UsernameNotUnique = Error.Conflict(
        "Users.UsernameNotUnique",
        "This username is already taken.");

    public static readonly Error InvalidCredentials = Error.Unauthorized(
        "Users.InvalidCredentials",
        "Invalid credentials.");

    public static readonly Error Unauthenticated = Error.Unauthorized(
        "Users.Unauthenticated",
        "You must be signed in to perform this action.");
}
