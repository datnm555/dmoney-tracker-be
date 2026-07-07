using Application.Abstractions.Messaging;
using Application.Users.Data;

namespace Application.Users;

public sealed record LoginCommand(string Identifier, string Password) : ICommand<LoginResponse>;
