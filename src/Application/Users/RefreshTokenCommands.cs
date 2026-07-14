using Application.Abstractions.Messaging;
using Application.Users.Data;

namespace Application.Users;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<LoginResponse>;

public sealed record LogoutCommand(string RefreshToken) : ICommand;
