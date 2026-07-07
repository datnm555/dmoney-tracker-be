using Application.Abstractions.Messaging;

namespace Application.Users;

public sealed record RegisterUserCommand(
    string Email,
    string Username,
    string DisplayName,
    string Password) : ICommand<Guid>;
